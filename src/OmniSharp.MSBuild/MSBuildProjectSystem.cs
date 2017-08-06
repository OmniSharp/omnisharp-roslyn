using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.FileWatching;
using OmniSharp.Models.Events;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild.Models;
using OmniSharp.MSBuild.Models.Events;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.MSBuild.Resolution;
using OmniSharp.MSBuild.SolutionParsing;
using OmniSharp.Options;
using OmniSharp.Services;

namespace OmniSharp.MSBuild
{
    [Export(typeof(IProjectSystem)), Shared]
    public class MSBuildProjectSystem : IProjectSystem
    {
        private readonly IOmniSharpEnvironment _environment;
        private readonly OmniSharpWorkspace _workspace;
        private readonly DotNetCliService _dotNetCli;
        private readonly MetadataFileReferenceCache _metadataFileReferenceCache;
        private readonly IEventEmitter _eventEmitter;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly PackageDependencyResolver _packageDepedencyResolver;

        private readonly object _gate = new object();
        private readonly Queue<ProjectFileInfo> _projectsToProcess;
        private readonly ProjectFileInfoCollection _projects;

        private MSBuildOptions _options;
        private string _solutionFileOrRootPath;

        public string Key { get; } = "MsBuild";
        public string Language { get; } = LanguageNames.CSharp;
        public IEnumerable<string> Extensions { get; } = new[] { ".cs" };

        [ImportingConstructor]
        public MSBuildProjectSystem(
            IOmniSharpEnvironment environment,
            OmniSharpWorkspace workspace,
            DotNetCliService dotNetCliService,
            MetadataFileReferenceCache metadataFileReferenceCache,
            IEventEmitter eventEmitter,
            IFileSystemWatcher fileSystemWatcher,
            ILoggerFactory loggerFactory)
        {
            _environment = environment;
            _workspace = workspace;
            _dotNetCli = dotNetCliService;
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _eventEmitter = eventEmitter;
            _fileSystemWatcher = fileSystemWatcher;
            _loggerFactory = loggerFactory;

            _projects = new ProjectFileInfoCollection();
            _projectsToProcess = new Queue<ProjectFileInfo>();
            _logger = loggerFactory.CreateLogger<MSBuildProjectSystem>();
            _packageDepedencyResolver = new PackageDependencyResolver(loggerFactory);
        }

        public void Initalize(IConfiguration configuration)
        {
            _options = new MSBuildOptions();
            ConfigurationBinder.Bind(configuration, _options);

            if (!MSBuildEnvironment.IsInitialized)
            {
                MSBuildEnvironment.Initialize(_logger);

                if (MSBuildEnvironment.IsInitialized &&
                    _environment.LogLevel < LogLevel.Information)
                {
                    var buildEnvironmentInfo = MSBuildHelpers.GetBuildEnvironmentInfo();
                    _logger.LogDebug($"MSBuild environment: {Environment.NewLine}{buildEnvironmentInfo}");
                }
            }

            var initialProjectPaths = GetInitialProjectPaths();

            foreach (var projectPath in initialProjectPaths)
            {
                if (!File.Exists(projectPath))
                {
                    _logger.LogWarning($"Found project that doesn't exist on disk: {projectPath}");
                    continue;
                }

                var project = LoadProject(projectPath);
                if (project == null)
                {
                    // Diagnostics reported while loading the project have already been logged.
                    continue;
                }

                _projectsToProcess.Enqueue(project);
            }

            ProcessProjects();
        }

        private IEnumerable<string> GetInitialProjectPaths()
        {
            // If a solution was provided, use it.
            if (!string.IsNullOrEmpty(_environment.SolutionFilePath))
            {
                _solutionFileOrRootPath = _environment.SolutionFilePath;
                return GetProjectPathsFromSolution(_environment.SolutionFilePath);
            }

            // Otherwise, assume that the path provided is a directory and look for a solution there.
            var solutionFilePath = FindSolutionFilePath(_environment.TargetDirectory, _logger);
            if (!string.IsNullOrEmpty(solutionFilePath))
            {
                _solutionFileOrRootPath = solutionFilePath;
                return GetProjectPathsFromSolution(solutionFilePath);
            }

            // Finally, if there isn't a single solution immediately available,
            // Just process all of the projects beneath the root path.
            _solutionFileOrRootPath = _environment.TargetDirectory;
            return Directory.GetFiles(_environment.TargetDirectory, "*.csproj", SearchOption.AllDirectories);
        }

        private IEnumerable<string> GetProjectPathsFromSolution(string solutionFilePath)
        {
            _logger.LogInformation($"Detecting projects in '{solutionFilePath}'.");

            var solutionFile = SolutionFile.ParseFile(solutionFilePath);
            var processedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var project in solutionFile.Projects)
            {
                if (project.IsSolutionFolder)
                {
                    continue;
                }

                // Solution files are assumed to contain relative paths to project files with Windows-style slashes.
                var projectFilePath = project.RelativePath.Replace('\\', Path.DirectorySeparatorChar);
                projectFilePath = Path.Combine(_environment.TargetDirectory, projectFilePath);
                projectFilePath = Path.GetFullPath(projectFilePath);

                // Have we seen this project? If so, move on.
                if (processedProjects.Contains(projectFilePath))
                {
                    continue;
                }

                if (string.Equals(Path.GetExtension(projectFilePath), ".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(projectFilePath);
                }

                processedProjects.Add(projectFilePath);
            }

            return result;
        }

        private void ProcessProjects()
        {
            while (_projectsToProcess.Count > 0)
            {
                var newProjects = new List<ProjectFileInfo>();

                while (_projectsToProcess.Count > 0)
                {
                    var project = _projectsToProcess.Dequeue();

                    if (!_projects.ContainsKey(project.FilePath))
                    {
                        AddProject(project);
                    }
                    else
                    {
                        _projects[project.FilePath] = project;
                    }

                    newProjects.Add(project);
                }

                // Next, update all projects.
                foreach (var project in newProjects)
                {
                    UpdateProject(project);
                }

                // Finally, check for any unresolved dependencies in the projects we just processes.
                foreach (var project in newProjects)
                {
                    CheckForUnresolvedDependences(project, allowAutoRestore: true);
                }
            }
        }

        private void AddProject(ProjectFileInfo project)
        {
            _projects.Add(project);

            var compilationOptions = CreateCompilationOptions(project);

            var projectInfo = ProjectInfo.Create(
                id: project.Id,
                version: VersionStamp.Create(),
                name: project.Name,
                assemblyName: project.AssemblyName,
                language: LanguageNames.CSharp,
                filePath: project.FilePath,
                outputFilePath: project.TargetPath,
                compilationOptions: compilationOptions);

            _workspace.AddProject(projectInfo);

            WatchProject(project);
        }

        private void WatchProject(ProjectFileInfo project)
        {
            // TODO: This needs some improvement. Currently, it tracks both deletions and changes
            // as "updates". We should properly remove projects that are deleted.
            _fileSystemWatcher.Watch(project.FilePath, file =>
            {
                OnProjectChanged(project.FilePath, allowAutoRestore: true);
            });

            if (!string.IsNullOrEmpty(project.ProjectAssetsFile))
            {
                _fileSystemWatcher.Watch(project.ProjectAssetsFile, file =>
                {
                    OnProjectChanged(project.FilePath, allowAutoRestore: false);
                });
            }
        }

        private static CSharpCompilationOptions CreateCompilationOptions(ProjectFileInfo projectFileInfo)
        {
            var result = new CSharpCompilationOptions(projectFileInfo.OutputKind);

            result = result.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);

            if (projectFileInfo.AllowUnsafeCode)
            {
                result = result.WithAllowUnsafe(true);
            }

            var specificDiagnosticOptions = new Dictionary<string, ReportDiagnostic>(projectFileInfo.SuppressedDiagnosticIds.Count)
            {
                // Ensure that specific warnings about assembly references are always suppressed.
                { "CS1701", ReportDiagnostic.Suppress },
                { "CS1702", ReportDiagnostic.Suppress },
                { "CS1705", ReportDiagnostic.Suppress }
            };

            if (projectFileInfo.SuppressedDiagnosticIds.Any())
            {
                foreach (var id in projectFileInfo.SuppressedDiagnosticIds)
                {
                    if (!specificDiagnosticOptions.ContainsKey(id))
                    {
                        specificDiagnosticOptions.Add(id, ReportDiagnostic.Suppress);
                    }
                }
            }

            result = result.WithSpecificDiagnosticOptions(specificDiagnosticOptions);

            if (projectFileInfo.SignAssembly && !string.IsNullOrEmpty(projectFileInfo.AssemblyOriginatorKeyFile))
            {
                var keyFile = Path.Combine(projectFileInfo.Directory, projectFileInfo.AssemblyOriginatorKeyFile);
                result = result.WithStrongNameProvider(new DesktopStrongNameProvider())
                               .WithCryptoKeyFile(keyFile);
            }

            if (!string.IsNullOrWhiteSpace(projectFileInfo.DocumentationFile))
            {
                result = result.WithXmlReferenceResolver(XmlFileResolver.Default);
            }

            return result;
        }

        private static string FindSolutionFilePath(string rootPath, ILogger logger)
        {
            var solutionsFilePaths = Directory.GetFiles(rootPath, "*.sln");
            var result = SolutionSelector.Pick(solutionsFilePaths, rootPath);

            if (result.Message != null)
            {
                logger.LogInformation(result.Message);
            }

            return result.FilePath;
        }

        private string GetSdksPath(string projectFilePath)
        {
            var info = _dotNetCli.GetInfo(Path.GetDirectoryName(projectFilePath));

            if (info.IsEmpty || string.IsNullOrWhiteSpace(info.BasePath))
            {
                return null;
            }

            var result = Path.Combine(info.BasePath, "Sdks");

            return Directory.Exists(result)
                ? result
                : null;
        }

        private ProjectFileInfo LoadProject(string projectFilePath)
        {
            _logger.LogInformation($"Loading project: {projectFilePath}");

            ProjectFileInfo project;
            var diagnostics = new List<MSBuildDiagnosticsMessage>();

            try
            {
                project = ProjectFileInfo.Create(projectFilePath, _environment.TargetDirectory, GetSdksPath(projectFilePath), _loggerFactory.CreateLogger<ProjectFileInfo>(), _options, diagnostics);

                if (project == null)
                {
                    _logger.LogWarning($"Failed to load project file '{projectFilePath}'.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to load project file '{projectFilePath}'.", ex);
                _eventEmitter.Error(ex, fileName: projectFilePath);
                project = null;
            }

            _eventEmitter.MSBuildProjectDiagnostics(projectFilePath, diagnostics);

            return project;
        }

        private void OnProjectChanged(string projectFilePath, bool allowAutoRestore)
        {
            lock (_gate)
            {
                if (_projects.TryGetValue(projectFilePath, out var oldProjectFileInfo))
                {
                    var diagnostics = new List<MSBuildDiagnosticsMessage>();
                    var newProjectFileInfo = oldProjectFileInfo.Reload(_environment.TargetDirectory, GetSdksPath(projectFilePath), _loggerFactory.CreateLogger<ProjectFileInfo>(), _options, diagnostics);

                    if (newProjectFileInfo != null)
                    {
                        _projects[projectFilePath] = newProjectFileInfo;

                        UpdateProject(newProjectFileInfo);
                        CheckForUnresolvedDependences(newProjectFileInfo, allowAutoRestore);
                    }
                }

                ProcessProjects();
            }
        }

        private void UpdateProject(ProjectFileInfo projectFileInfo)
        {
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
                if (!_projects.TryGetValue(projectReferencePath, out var referencedProject))
                {
                    if (File.Exists(projectReferencePath))
                    {
                        _logger.LogInformation($"Found referenced project outside root directory: {projectReferencePath}");

                        // We've found a project reference that we didn't know about already, but it exists on disk.
                        // This is likely a project that is outside of OmniSharp's TargetDirectory.
                        referencedProject = LoadProject(projectReferencePath);

                        if (referencedProject != null)
                        {
                            AddProject(referencedProject);

                            // Ensure this project is queued to be updated later.
                            _projectsToProcess.Enqueue(referencedProject);
                        }
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

        private void UpdateReferences(Project project, ImmutableArray<string> references)
        {
            var existingReferences = new HashSet<MetadataReference>(project.MetadataReferences);
            var addedReferences = new HashSet<MetadataReference>();

            foreach (var referencePath in references)
            {
                if (!File.Exists(referencePath))
                {
                    _logger.LogWarning($"Unable to resolve assembly '{referencePath}'");
                }
                else
                {
                    var metadataReference = _metadataFileReferenceCache.GetMetadataReference(referencePath);

                    if (existingReferences.Remove(metadataReference))
                    {
                        continue;
                    }

                    if (!addedReferences.Contains(metadataReference))
                    {
                        _logger.LogDebug($"Adding reference '{referencePath}' to '{project.Name}'.");
                        _workspace.AddMetadataReference(project.Id, metadataReference);
                        addedReferences.Add(metadataReference);
                    }
                }
            }

            foreach (var existingReference in existingReferences)
            {
                _workspace.RemoveMetadataReference(project.Id, existingReference);
            }
        }

        private void CheckForUnresolvedDependences(ProjectFileInfo projectFile, bool allowAutoRestore)
        {
            var unresolvedPackageReferences = _packageDepedencyResolver.FindUnresolvedPackageReferences(projectFile);
            if (unresolvedPackageReferences.IsEmpty)
            {
                return;
            }

            var unresolvedDependencies = unresolvedPackageReferences.Select(packageReference =>
                new PackageDependency
                {
                    Name = packageReference.Dependency.Id,
                    Version = packageReference.Dependency.VersionRange.ToNormalizedString()
                });

            if (allowAutoRestore && _options.EnablePackageAutoRestore)
            {
                _dotNetCli.RestoreAsync(projectFile.Directory, onFailure: () =>
                {
                    _eventEmitter.UnresolvedDepdendencies(projectFile.FilePath, unresolvedDependencies);
                });
            }
            else
            {
                _eventEmitter.UnresolvedDepdendencies(projectFile.FilePath, unresolvedDependencies);
            }
        }

        private ProjectFileInfo GetProjectFileInfo(string path)
        {
            if (!_projects.TryGetValue(path, out var projectFileInfo))
            {
                return null;
            }

            return projectFileInfo;
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            var info = new MSBuildWorkspaceInfo(
                _solutionFileOrRootPath, _projects,
                excludeSourceFiles: request?.ExcludeSourceFiles ?? false);

            return Task.FromResult<object>(info);
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            var document = _workspace.GetDocument(filePath);

            var projectFilePath = document != null
                ? document.Project.FilePath
                : filePath;

            var projectFileInfo = GetProjectFileInfo(projectFilePath);
            if (projectFileInfo == null)
            {
                _logger.LogDebug($"Could not locate project for '{projectFilePath}'");
                return Task.FromResult<object>(null);
            }

            var info = new MSBuildProjectInfo(projectFileInfo);

            return Task.FromResult<object>(info);
        }
    }
}
