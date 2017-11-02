using System;
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
using OmniSharp.Eventing;
using OmniSharp.FileWatching;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.MSBuild.Logging;
using OmniSharp.MSBuild.Models.Events;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild
{
    internal class ProjectManager : DisposableObject
    {
        private readonly ILogger _logger;
        private readonly IEventEmitter _eventEmitter;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly MetadataFileReferenceCache _metadataFileReferenceCache;
        private readonly PackageDependencyChecker _packageDependencyChecker;
        private readonly ProjectFileInfoCollection _projectFiles;
        private readonly ProjectLoader _projectLoader;
        private readonly OmniSharpWorkspace _workspace;

        private const int LoopDelay = 100; // milliseconds
        private readonly BufferBlock<string> _queue;
        private readonly CancellationTokenSource _processLoopCancellation;
        private readonly Task _processLoopTask;
        private bool _processingQueue;

        public ProjectManager(ILoggerFactory loggerFactory, IEventEmitter eventEmitter, IFileSystemWatcher fileSystemWatcher, MetadataFileReferenceCache metadataFileReferenceCache, PackageDependencyChecker packageDependencyChecker, ProjectLoader projectLoader, OmniSharpWorkspace workspace)
        {
            _logger = loggerFactory.CreateLogger<ProjectManager>();
            _eventEmitter = eventEmitter;
            _fileSystemWatcher = fileSystemWatcher;
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _packageDependencyChecker = packageDependencyChecker;
            _projectFiles = new ProjectFileInfoCollection();
            _projectLoader = projectLoader;
            _workspace = workspace;

            _queue = new BufferBlock<string>();
            _processLoopCancellation = new CancellationTokenSource();
            _processLoopTask = Task.Run(() => ProcessLoopAsync(_processLoopCancellation.Token));
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

        public void QueueProjectUpdate(string projectFilePath)
        {
            _logger.LogInformation($"Queue project update for '{projectFilePath}'");
            _queue.Post(projectFilePath);
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
                HashSet<string> processedSet = null;

                while (_queue.TryReceive(out var projectFilePath))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (processedSet == null)
                    {
                        processedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }

                    // Ensure that we don't process the same project twice.
                    if (!processedSet.Add(projectFilePath))
                    {
                        continue;
                    }

                    // TODO: Handle removing project

                    // update or add project
                    if (_projectFiles.TryGetValue(projectFilePath, out var projectFileInfo))
                    {
                        projectFileInfo = ReloadProject(projectFileInfo);
                        _projectFiles[projectFilePath] = projectFileInfo;
                    }
                    else
                    {
                        projectFileInfo = LoadProject(projectFilePath);
                        AddProject(projectFileInfo);
                    }
                }

                if (processedSet != null)
                {
                    foreach (var projectFilePath in processedSet)
                    {
                        UpdateProject(projectFilePath);
                    }

                    foreach (var projectFilePath in processedSet)
                    {
                        if (_projectFiles.TryGetValue(projectFilePath, out var projectFileInfo))
                        {
                            _packageDependencyChecker.CheckForUnresolvedDependences(projectFileInfo, allowAutoRestore: true);
                        }
                    }
                }
            }
            finally
            {
                _processingQueue = false;
            }
        }

        private ProjectFileInfo LoadProject(string projectFilePath)
            => LoadOrReloadProject(projectFilePath, () => ProjectFileInfo.Load(projectFilePath, _projectLoader));

        private ProjectFileInfo ReloadProject(ProjectFileInfo projectFileInfo)
            => LoadOrReloadProject(projectFileInfo.FilePath, () => projectFileInfo.Reload(_projectLoader));

        private ProjectFileInfo LoadOrReloadProject(string projectFilePath, Func<(ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>)> loadFunc)
        {
            _logger.LogInformation($"Loading project: {projectFilePath}");

            ProjectFileInfo projectFileInfo;
            ImmutableArray<MSBuildDiagnostic> diagnostics;

            try
            {
                (projectFileInfo, diagnostics) = loadFunc();

                if (projectFileInfo == null)
                {
                    _logger.LogWarning($"Failed to load project file '{projectFilePath}'.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to load project file '{projectFilePath}'.", ex);
                _eventEmitter.Error(ex, fileName: projectFilePath);
                projectFileInfo = null;
            }

            _eventEmitter.MSBuildProjectDiagnostics(projectFilePath, diagnostics);

            return projectFileInfo;
        }

        private bool RemoveProject(string projectFilePath)
        {
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

            var projectInfo = projectFileInfo.CreateProjectInfo();
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
                QueueProjectUpdate(projectFileInfo.FilePath);
            });

            if (!string.IsNullOrEmpty(projectFileInfo.ProjectAssetsFile))
            {
                _fileSystemWatcher.Watch(projectFileInfo.ProjectAssetsFile, (file, changeType) =>
                {
                    QueueProjectUpdate(projectFileInfo.FilePath);
                });

                var restoreDirectory = Path.GetDirectoryName(projectFileInfo.ProjectAssetsFile);
                var nugetFileBase = Path.Combine(restoreDirectory, Path.GetFileName(projectFileInfo.FilePath) + ".nuget");
                var nugetCacheFile = nugetFileBase + ".cache";
                var nugetPropsFile = nugetFileBase + ".g.props";
                var nugetTargetsFile = nugetFileBase + ".g.targets";

                _fileSystemWatcher.Watch(nugetCacheFile, (file, changeType) =>
                {
                    QueueProjectUpdate(projectFileInfo.FilePath);
                });

                _fileSystemWatcher.Watch(nugetPropsFile, (file, changeType) =>
                {
                    QueueProjectUpdate(projectFileInfo.FilePath);
                });

                _fileSystemWatcher.Watch(nugetTargetsFile, (file, changeType) =>
                {
                    QueueProjectUpdate(projectFileInfo.FilePath);
                });
            }
        }

        private void UpdateProject(string projectFilePath)
        {
            if (!_projectFiles.TryGetValue(projectFilePath, out var projectFileInfo))
            {
                _logger.LogError($"Attemped to update project that is not loaded: {projectFilePath}");
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
            UpdateReferences(project, projectFileInfo.References);
        }

        private void UpdateSourceFiles(Project project, IList<string> sourceFiles)
        {
            var currentDocuments = project.Documents.ToDictionary(d => d.FilePath, d => d.Id);

            // Add source files to the project.
            foreach (var sourceFile in sourceFiles)
            {
                _fileSystemWatcher.Watch(Path.GetDirectoryName(sourceFile), OnDirectoryFileChanged);

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
                if (!_projectFiles.TryGetValue(projectReferencePath, out var referencedProject))
                {
                    if (File.Exists(projectReferencePath))
                    {
                        _logger.LogInformation($"Found referenced project outside root directory: {projectReferencePath}");

                        // We've found a project reference that we didn't know about already, but it exists on disk.
                        // This is likely a project that is outside of OmniSharp's TargetDirectory.
                        referencedProject = ProjectFileInfo.CreateNoBuild(projectReferencePath, _projectLoader);
                        AddProject(referencedProject);

                        QueueProjectUpdate(projectReferencePath);
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

        private class MetadataReferenceComparer : IEqualityComparer<MetadataReference>
        {
            public static MetadataReferenceComparer Instance { get; } = new MetadataReferenceComparer();

            public bool Equals(MetadataReference x, MetadataReference y)
                => x is PortableExecutableReference pe1 && y is PortableExecutableReference pe2
                    ? StringComparer.OrdinalIgnoreCase.Equals(pe1.FilePath, pe2.FilePath)
                    : EqualityComparer<MetadataReference>.Default.Equals(x, y);

            public int GetHashCode(MetadataReference obj)
                => obj is PortableExecutableReference pe
                    ? StringComparer.OrdinalIgnoreCase.GetHashCode(pe.FilePath)
                    : EqualityComparer<MetadataReference>.Default.GetHashCode(obj);
        }

        private void UpdateReferences(Project project, ImmutableArray<string> referencePaths)
        {
            var referencesToRemove = new HashSet<MetadataReference>(project.MetadataReferences, MetadataReferenceComparer.Instance);
            var referencesToAdd = new HashSet<MetadataReference>(MetadataReferenceComparer.Instance);

            foreach (var referencePath in referencePaths)
            {
                if (!File.Exists(referencePath))
                {
                    _logger.LogWarning($"Unable to resolve assembly '{referencePath}'");
                }
                else
                {
                    var reference = _metadataFileReferenceCache.GetMetadataReference(referencePath);

                    if (referencesToRemove.Remove(reference))
                    {
                        continue;
                    }

                    if (!referencesToAdd.Contains(reference))
                    {
                        _logger.LogDebug($"Adding reference '{referencePath}' to '{project.Name}'.");
                        _workspace.AddMetadataReference(project.Id, reference);
                        referencesToAdd.Add(reference);
                    }
                }
            }

            foreach (var reference in referencesToRemove)
            {
                _workspace.RemoveMetadataReference(project.Id, reference);
            }
        }
    }
}
