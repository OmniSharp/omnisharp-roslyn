using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Eventing;
using OmniSharp.FileWatching;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.MSBuild.Logging;
using OmniSharp.MSBuild.Models.Events;
using OmniSharp.MSBuild.Notification;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Options;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild
{
    internal class ProjectManager : DisposableObject
    {
        private class ProjectToUpdate
        {
            public ProjectIdInfo ProjectIdInfo;
            public string FilePath { get; }
            public bool AllowAutoRestore { get; set; }
            public ProjectLoadedEventArgs LoadedEventArgs { get; set; }

            public ProjectToUpdate(string filePath, bool allowAutoRestore, ProjectIdInfo projectIdInfo)
            {
                ProjectIdInfo = projectIdInfo ?? throw new ArgumentNullException(nameof(projectIdInfo));
                FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
                AllowAutoRestore = allowAutoRestore;
            }
        }

        private readonly ILogger _logger;
        private readonly MSBuildOptions _options;
        private readonly IEventEmitter _eventEmitter;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly MetadataFileReferenceCache _metadataFileReferenceCache;
        private readonly PackageDependencyChecker _packageDependencyChecker;
        private readonly ProjectFileInfoCollection _projectFiles;
        private readonly HashSet<string> _failedToLoadProjectFiles;
        private readonly ConcurrentDictionary<string, int/*unused*/> _projectsRequestedOnDemand;
        private readonly ProjectLoader _projectLoader;
        private readonly OmniSharpWorkspace _workspace;
        private readonly CachingCodeFixProviderForProjects _codeFixesForProject;
        private readonly ImmutableArray<IMSBuildEventSink> _eventSinks;
        private const int LoopDelay = 100; // milliseconds
        private readonly BufferBlock<ProjectToUpdate> _queue;
        private readonly CancellationTokenSource _processLoopCancellation;
        private readonly Task _processLoopTask;
        private readonly IAnalyzerAssemblyLoader _assemblyLoader;
        private bool _processingQueue;

        private readonly FileSystemNotificationCallback _onDirectoryFileChanged;
        private readonly RulesetsForProjects _rulesetsForProjects;

        public ProjectManager(
            ILoggerFactory loggerFactory,
            MSBuildOptions options,
            IEventEmitter eventEmitter,
            IFileSystemWatcher fileSystemWatcher,
            MetadataFileReferenceCache metadataFileReferenceCache,
            PackageDependencyChecker packageDependencyChecker,
            ProjectLoader projectLoader,
            OmniSharpWorkspace workspace,
            CachingCodeFixProviderForProjects codeFixesForProject,
            RulesetsForProjects rulesetsForProjects,
            IAnalyzerAssemblyLoader assemblyLoader,
            ImmutableArray<IMSBuildEventSink> eventSinks)
        {
            _logger = loggerFactory.CreateLogger<ProjectManager>();
            _options = options ?? new MSBuildOptions();
            _eventEmitter = eventEmitter;
            _fileSystemWatcher = fileSystemWatcher;
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _packageDependencyChecker = packageDependencyChecker;
            _projectFiles = new ProjectFileInfoCollection();
            _failedToLoadProjectFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _projectsRequestedOnDemand = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _projectLoader = projectLoader;
            _workspace = workspace;
            _codeFixesForProject = codeFixesForProject;
            _eventSinks = eventSinks;
            _queue = new BufferBlock<ProjectToUpdate>();
            _processLoopCancellation = new CancellationTokenSource();
            _processLoopTask = Task.Run(() => ProcessLoopAsync(_processLoopCancellation.Token));
            _assemblyLoader = assemblyLoader;
            _onDirectoryFileChanged = OnDirectoryFileChanged;
            _rulesetsForProjects = rulesetsForProjects;

            if (_options.LoadProjectsOnDemand)
            {
                _workspace.AddWaitForProjectModelReadyHandler(WaitForProjectModelReadyAsync);
            }
        }

        private async Task WaitForProjectModelReadyAsync(string documentPath)
        {
            // Search and queue for loading C# projects that are likely to reference the requested file.
            // C# source files are located pretty much always in the same folder with their project file or in a subfolder below.
            // Search up the root folder to enable on-demand project load in additional scenarios like the following:
            // - A subfolder in a big codebase was opened in VSCode and then a document was opened that is located outside of the subfoler.
            // - A workspace was opened in VSCode that includes multiple subfolders from a big codebase.
            // - Documents from different codebases are opened in the same VSCode workspace.
            string projectDir = Path.GetDirectoryName(documentPath);
            do
            {
                var csProjFiles = Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly).ToList();
                if (csProjFiles.Count > 0)
                {
                    foreach(string csProjFile in csProjFiles)
                    {
                        if (_projectsRequestedOnDemand.TryAdd(csProjFile, 0 /*unused*/))
                        {
                            var projectIdInfo = new ProjectIdInfo(ProjectId.CreateNewId(csProjFile), false);
                            QueueProjectUpdate(csProjFile, allowAutoRestore:true, projectIdInfo);
                        }
                    }

                    break;
                }

                projectDir = Path.GetDirectoryName(projectDir);
            } while(projectDir != null);

            // Wait for all queued projects to load to ensure that workspace is fully up to date before this method completes.
            // If the project for the document was loaded before and there are no other projects to load at the moment, the call below will be no-op.
            _logger.LogTrace($"Started waiting for projects queue to be empty when requested '{documentPath}'");
            await WaitForQueueEmptyAsync();
            _logger.LogTrace($"Stopped waiting for projects queue to be empty when requested '{documentPath}'");
        }

        protected override void DisposeCore(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }

            _processLoopCancellation.Cancel();
            _processLoopCancellation.Dispose();
        }

        public IEnumerable<ProjectFileInfo> GetAllProjects() => _projectFiles.GetItems();
        public bool TryGetProject(string projectFilePath, out ProjectFileInfo projectFileInfo) => _projectFiles.TryGetValue(projectFilePath, out projectFileInfo);

        public void QueueProjectUpdate(string projectFilePath, bool allowAutoRestore, ProjectIdInfo projectId)
        {
            _logger.LogInformation($"Queue project update for '{projectFilePath}'");
            _queue.Post(new ProjectToUpdate(projectFilePath, allowAutoRestore, projectId));
        }

        public async Task WaitForQueueEmptyAsync()
        {
            while (_queue.Count > 0 || _processingQueue)
            {
                await Task.Delay(LoopDelay);
            }
        }

        private async Task ProcessLoopAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                await Task.Delay(LoopDelay, cancellationToken);
                ProcessQueue(cancellationToken);
            }
        }

        private void ProcessQueue(CancellationToken cancellationToken)
        {
            _processingQueue = true;
            try
            {
                Dictionary<string, ProjectToUpdate> projectByFilePathMap = null;
                List<ProjectToUpdate> projectList = null;

                while (_queue.TryReceive(out var currentProject))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (projectByFilePathMap == null)
                    {
                        projectByFilePathMap = new Dictionary<string, ProjectToUpdate>(StringComparer.OrdinalIgnoreCase);
                        projectList = new List<ProjectToUpdate>();
                    }

                    // Ensure that we don't process the same project twice. However, if a project *does*
                    // appear more than once in the update queue, ensure that AllowAutoRestore is set to true
                    // if any of the updates requires it.
                    if (projectByFilePathMap.TryGetValue(currentProject.FilePath, out var trackedProject))
                    {
                        if (currentProject.AllowAutoRestore && !trackedProject.AllowAutoRestore)
                        {
                            trackedProject.AllowAutoRestore = true;
                        }

                        continue;
                    }

                    // TODO: Handle removing project

                    projectByFilePathMap.Add(currentProject.FilePath, currentProject);
                    projectList.Add(currentProject);

                    // update or add project
                    _failedToLoadProjectFiles.Remove(currentProject.FilePath);

                    ProjectFileInfo projectFileInfo;
                    ProjectLoadedEventArgs loadedEventArgs;

                    if (_projectFiles.TryGetValue(currentProject.FilePath, out projectFileInfo))
                    {
                        (projectFileInfo, loadedEventArgs) = ReloadProject(projectFileInfo);
                        if (projectFileInfo == null)
                        {
                            _failedToLoadProjectFiles.Add(currentProject.FilePath);
                            continue;
                        }

                        currentProject.LoadedEventArgs = loadedEventArgs;
                        _projectFiles[currentProject.FilePath] = projectFileInfo;
                    }
                    else
                    {
                        (projectFileInfo, loadedEventArgs) = LoadProject(currentProject.FilePath, currentProject.ProjectIdInfo);
                        if (projectFileInfo == null)
                        {
                            _failedToLoadProjectFiles.Add(currentProject.FilePath);
                            continue;
                        }

                        currentProject.LoadedEventArgs = loadedEventArgs;
                        AddProject(projectFileInfo);
                    }
                }

                if (projectByFilePathMap != null)
                {
                    foreach (var project in projectList)
                    {
                        UpdateProject(project.FilePath);

                        // Fire loaded events
                        if (project.LoadedEventArgs != null)
                        {
                            foreach (var eventSink in _eventSinks)
                            {
                                try
                                {
                                    eventSink.ProjectLoaded(project.LoadedEventArgs);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Exception thrown while calling event sinks");
                                }
                            }
                        }
                    }

                    foreach (var project in projectList)
                    {
                        if (_projectFiles.TryGetValue(project.FilePath, out var projectFileInfo))
                        {
                            _packageDependencyChecker.CheckForUnresolvedDependences(projectFileInfo, project.AllowAutoRestore);
                        }
                    }
                }
            }
            finally
            {
                _processingQueue = false;
            }

            _fileSystemWatcher.Watch(".cs", _onDirectoryFileChanged);
        }

        private (ProjectFileInfo, ProjectLoadedEventArgs) LoadProject(string projectFilePath, ProjectIdInfo idInfo)
            => LoadOrReloadProject(projectFilePath, () => ProjectFileInfo.Load(projectFilePath, idInfo, _projectLoader));

        private (ProjectFileInfo, ProjectLoadedEventArgs) ReloadProject(ProjectFileInfo projectFileInfo)
            => LoadOrReloadProject(projectFileInfo.FilePath, () => projectFileInfo.Reload(_projectLoader));

        private (ProjectFileInfo, ProjectLoadedEventArgs) LoadOrReloadProject(string projectFilePath, Func<(ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>, ProjectLoadedEventArgs)> loader)
        {
            _logger.LogInformation($"Loading project: {projectFilePath}");

            try
            {
                var (projectFileInfo, diagnostics, eventArgs) = loader();

                if (projectFileInfo != null)
                {
                    _logger.LogInformation($"Successfully loaded project file '{projectFilePath}'.");
                }
                else
                {
                    _logger.LogWarning($"Failed to load project file '{projectFilePath}'.");
                }

                _eventEmitter.MSBuildProjectDiagnostics(projectFilePath, diagnostics);

                return (projectFileInfo, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to load project file '{projectFilePath}'.", ex);
                _eventEmitter.Error(ex, fileName: projectFilePath);
                return (null, null);
            }
        }

        private bool RemoveProject(string projectFilePath)
        {
            _failedToLoadProjectFiles.Remove(projectFilePath);
            if (!_projectFiles.TryGetValue(projectFilePath, out var projectFileInfo))
            {
                return false;
            }

            _projectFiles.Remove(projectFilePath);

            var newSolution = _workspace.CurrentSolution.RemoveProject(projectFileInfo.Id);

            if (!_workspace.TryApplyChanges(newSolution))
            {
                _logger.LogError($"Failed to remove project from workspace: '{projectFileInfo.FilePath}'");
            }

            // TODO: Stop watching project files

            return true;
        }

        private void AddProject(ProjectFileInfo projectFileInfo)
        {
            _logger.LogInformation($"Adding project '{projectFileInfo.FilePath}'");

            _projectFiles.Add(projectFileInfo);

            var projectInfo = projectFileInfo.CreateProjectInfo(_assemblyLoader);

            _codeFixesForProject.LoadFrom(projectInfo);

            if(projectFileInfo.RuleSet != null)
                _rulesetsForProjects.AddOrUpdateRuleset(projectFileInfo.Id, projectFileInfo.RuleSet);

            var newSolution = _workspace.CurrentSolution.AddProject(projectInfo);

            if (!_workspace.TryApplyChanges(newSolution))
            {
                _logger.LogError($"Failed to add project to workspace: '{projectFileInfo.FilePath}'");
            }

            WatchProjectFiles(projectFileInfo);
        }

        private void WatchProjectFiles(ProjectFileInfo projectFileInfo)
        {
            // TODO: This needs some improvement. Currently, it tracks both deletions and changes
            // as "updates". We should properly remove projects that are deleted.
            _fileSystemWatcher.Watch(projectFileInfo.FilePath, (file, changeType) =>
            {
                QueueProjectUpdate(projectFileInfo.FilePath, allowAutoRestore: true, projectFileInfo.ProjectIdInfo);
            });

            if (!string.IsNullOrEmpty(projectFileInfo.ProjectAssetsFile))
            {
                _fileSystemWatcher.Watch(projectFileInfo.ProjectAssetsFile, (file, changeType) =>
                {
                    QueueProjectUpdate(projectFileInfo.FilePath, allowAutoRestore: false, projectFileInfo.ProjectIdInfo);
                });

                var restoreDirectory = Path.GetDirectoryName(projectFileInfo.ProjectAssetsFile);
                var nugetFileBase = Path.Combine(restoreDirectory, Path.GetFileName(projectFileInfo.FilePath) + ".nuget");
                var nugetCacheFile = nugetFileBase + ".cache";
                var nugetPropsFile = nugetFileBase + ".g.props";
                var nugetTargetsFile = nugetFileBase + ".g.targets";

                _fileSystemWatcher.Watch(nugetCacheFile, (file, changeType) =>
                {
                    QueueProjectUpdate(projectFileInfo.FilePath, allowAutoRestore: false, projectFileInfo.ProjectIdInfo);
                });

                _fileSystemWatcher.Watch(nugetPropsFile, (file, changeType) =>
                {
                    QueueProjectUpdate(projectFileInfo.FilePath, allowAutoRestore: false, projectFileInfo.ProjectIdInfo);
                });

                _fileSystemWatcher.Watch(nugetTargetsFile, (file, changeType) =>
                {
                    QueueProjectUpdate(projectFileInfo.FilePath, allowAutoRestore: false, projectFileInfo.ProjectIdInfo);
                });
            }
        }

        private void UpdateProject(string projectFilePath)
        {
            if (!_projectFiles.TryGetValue(projectFilePath, out var projectFileInfo))
            {
                _logger.LogError($"Attempted to update project that is not loaded: {projectFilePath}");
                return;
            }

            var project = _workspace.CurrentSolution.GetProject(projectFileInfo.Id);
            if (project == null)
            {
                _logger.LogError($"Could not locate project in workspace: {projectFileInfo.FilePath}");
                return;
            }

            UpdateSourceFiles(project, projectFileInfo.SourceFiles);
            UpdateParseOptions(project, projectFileInfo.LanguageVersion, projectFileInfo.PreprocessorSymbolNames, !string.IsNullOrWhiteSpace(projectFileInfo.DocumentationFile));
            UpdateProjectReferences(project, projectFileInfo.ProjectReferences);
            UpdateReferences(project, projectFileInfo.ProjectReferences, projectFileInfo.References);
            _workspace.TryPromoteMiscellaneousDocumentsToProject(project);
        }

        private void UpdateSourceFiles(Project project, IList<string> sourceFiles)
        {
            // Remove transient documents from list of current documents, to assure proper new documents are added.
            // Transient documents will be removed on workspace DocumentAdded event.
            var currentDocuments = project.Documents
                .Where(document => !_workspace.BufferManager.IsTransientDocument(document.Id))
                .ToDictionary(d => d.FilePath, d => d.Id);

            // Add source files to the project.
            foreach (var sourceFile in sourceFiles)
            {
                // If a document for this source file already exists in the project, carry on.
                if (currentDocuments.Remove(sourceFile))
                {
                    continue;
                }

                // If the source file doesn't exist on disk, don't try to add it.
                if (!File.Exists(sourceFile))
                {
                    continue;
                }

                _workspace.AddDocument(project.Id, sourceFile);
            }

            // Removing any remaining documents from the project.
            foreach (var currentDocument in currentDocuments)
            {
                _workspace.RemoveDocument(currentDocument.Value);
            }
        }

        private void OnDirectoryFileChanged(string path, FileChangeType changeType)
        {
            // Hosts may not have passed through a file change type
            if (changeType == FileChangeType.Unspecified && !File.Exists(path) || changeType == FileChangeType.Delete)
            {
                foreach (var documentId in _workspace.CurrentSolution.GetDocumentIdsWithFilePath(path))
                {
                    _workspace.RemoveDocument(documentId);
                }
            }

            if (changeType == FileChangeType.Unspecified || changeType == FileChangeType.Create)
            {
                // Only add cs files. Also, make sure the path is a file, and not a directory name that happens to end in ".cs"
                if (string.Equals(Path.GetExtension(path), ".cs", StringComparison.CurrentCultureIgnoreCase) && File.Exists(path))
                {
                    // Use the buffer manager to add the new file to the appropriate projects
                    // Hosts that don't pass the FileChangeType may wind up updating the buffer twice
                    _workspace.BufferManager.UpdateBufferAsync(new UpdateBufferRequest() { FileName = path, FromDisk = true }).Wait();
                }
            }
        }

        private void UpdateParseOptions(Project project, LanguageVersion languageVersion, IEnumerable<string> preprocessorSymbolNames, bool generateXmlDocumentation)
        {
            var existingParseOptions = (CSharpParseOptions)project.ParseOptions;

            if (existingParseOptions.LanguageVersion == languageVersion &&
                Enumerable.SequenceEqual(existingParseOptions.PreprocessorSymbolNames, preprocessorSymbolNames) &&
                (existingParseOptions.DocumentationMode == DocumentationMode.Diagnose) == generateXmlDocumentation)
            {
                // No changes to make. Moving on.
                return;
            }

            var parseOptions = new CSharpParseOptions(languageVersion);

            if (preprocessorSymbolNames.Any())
            {
                parseOptions = parseOptions.WithPreprocessorSymbols(preprocessorSymbolNames);
            }

            if (generateXmlDocumentation)
            {
                parseOptions = parseOptions.WithDocumentationMode(DocumentationMode.Diagnose);
            }

            _workspace.SetParseOptions(project.Id, parseOptions);
        }

        private void UpdateProjectReferences(Project project, ImmutableArray<string> projectReferencePaths)
        {
            _logger.LogInformation($"Update project: {project.Name}");

            var existingProjectReferences = new HashSet<ProjectReference>(project.ProjectReferences);
            var addedProjectReferences = new HashSet<ProjectReference>();

            foreach (var projectReferencePath in projectReferencePaths)
            {
                if (_failedToLoadProjectFiles.Contains(projectReferencePath))
                {
                    _logger.LogWarning($"Ignoring previously failed to load project '{projectReferencePath}' referenced by '{project.Name}'.");
                    continue;
                }

                if (!_projectFiles.TryGetValue(projectReferencePath, out var referencedProject))
                {
                    if (File.Exists(projectReferencePath) &&
                        projectReferencePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"Found referenced project outside root directory: {projectReferencePath}");

                        // We've found a project reference that we didn't know about already, but it exists on disk.
                        // This is likely a project that is outside of OmniSharp's TargetDirectory.
                        referencedProject = ProjectFileInfo.CreateNoBuild(projectReferencePath, _projectLoader);
                        AddProject(referencedProject);

                        QueueProjectUpdate(projectReferencePath, allowAutoRestore: true, referencedProject.ProjectIdInfo);
                    }
                }

                if (referencedProject == null)
                {
                    _logger.LogWarning($"Unable to resolve project reference '{projectReferencePath}' for '{project.Name}'.");
                    continue;
                }

                var projectReference = new ProjectReference(referencedProject.Id);

                if (existingProjectReferences.Remove(projectReference))
                {
                    // This reference already exists
                    continue;
                }

                if (!addedProjectReferences.Contains(projectReference))
                {
                    _workspace.AddProjectReference(project.Id, projectReference);
                    addedProjectReferences.Add(projectReference);
                }
            }

            foreach (var existingProjectReference in existingProjectReferences)
            {
                _workspace.RemoveProjectReference(project.Id, existingProjectReference);
            }
        }

        private void UpdateReferences(Project project, ImmutableArray<string> projectReferencePaths, ImmutableArray<string> referencePaths)
        {
            var referencesToRemove = new HashSet<MetadataReference>(project.MetadataReferences, MetadataReferenceEqualityComparer.Instance);
            var referencesToAdd = new HashSet<MetadataReference>(MetadataReferenceEqualityComparer.Instance);

            foreach (var referencePath in referencePaths)
            {
                if (!File.Exists(referencePath))
                {
                    _logger.LogWarning($"Unable to resolve assembly '{referencePath}'");
                    continue;
                }

                // There is no need to explicitly add assembly to workspace when the assembly is produced by a project reference.
                // Doing so actually can cause /codecheck request to return errors like below for types in the referenced project if it is for example signed:
                // The type 'TestClass' exists in both 'SignedLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null' and 'ClassLibrary1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=a5d85a5baa39a6a6'
                if (TryFindLoadedProjectReferenceWithTargetPath(referencePath, projectReferencePaths, project.Name, out ProjectFileInfo projectReferenceWithTarget))
                {
                    _logger.LogDebug($"Skipped reference {referencePath} of project {project.Name} because it is already represented as a target " +
                        $"of loaded project reference {projectReferenceWithTarget.Name}");
                    continue;
                }

                var reference = _metadataFileReferenceCache.GetMetadataReference(referencePath);

                if (referencesToRemove.Remove(reference))
                {
                    continue;
                }

                if (!referencesToAdd.Contains(reference))
                {
                    if (_projectFiles.TryGetValue(project.FilePath, out var projectFileInfo))
                    {
                        if (projectFileInfo.ReferenceAliases != null && projectFileInfo.ReferenceAliases.TryGetValue(referencePath, out var aliases))
                        {
                            if (!string.IsNullOrEmpty(aliases))
                            {
                                reference = reference.WithAliases(aliases.Split(';'));
                                _logger.LogDebug($"setting aliases: {referencePath}, {aliases} ");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug($"failed to get project info:{project.FilePath}");
                    }
                    _logger.LogDebug($"Adding reference '{referencePath}' to '{project.Name}'.");
                    _workspace.AddMetadataReference(project.Id, reference);
                    referencesToAdd.Add(reference);
                }
            }

            foreach (var reference in referencesToRemove)
            {
                _workspace.RemoveMetadataReference(project.Id, reference);
            }
        }

        /// <summary> Attempts to locate a referenced project with particular target path i.e. the path of the assembly that the referenced project produces. /// </summary>
        /// <param name="targetPath">Target path to look for.</param>
        /// <param name="projectReferencePaths">List of projects referenced by <see cref="projectName"/></param>
        /// <param name="projectName">Name of the project for which the search is initiated</param>
        /// <param name="projectReferenceWithTarget">If found, contains project reference with the <see cref="targetPath"/>; null otherwise</param>
        /// <returns></returns>
        private bool TryFindLoadedProjectReferenceWithTargetPath(string targetPath, ImmutableArray<string> projectReferencePaths, string projectName, out ProjectFileInfo projectReferenceWithTarget)
        {
            projectReferenceWithTarget = null;
            foreach (string projectReferencePath in projectReferencePaths)
            {
                if (!_projectFiles.TryGetValue(projectReferencePath, out ProjectFileInfo referencedProject))
                {
                    _logger.LogWarning($"Expected project reference {projectReferencePath} to be already loaded for project {projectName}");
                    continue;
                }

                if (referencedProject.TargetPath != null && referencedProject.TargetPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    projectReferenceWithTarget = referencedProject;
                    return true;
                }
            }

            return false;
        }
    }
}
