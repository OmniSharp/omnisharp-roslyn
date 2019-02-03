using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.FileSystem;
using OmniSharp.FileWatching;
using OmniSharp.Mef;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.MSBuild.Models;
using OmniSharp.MSBuild.Notification;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.MSBuild.SolutionParsing;
using OmniSharp.Options;
using OmniSharp.Services;
using System.Linq;

namespace OmniSharp.MSBuild
{
    [ExportProjectSystem(ProjectSystemNames.MSBuildProjectSystem), Shared]
    internal class ProjectSystem : IProjectSystem
    {
        private readonly IOmniSharpEnvironment _environment;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ImmutableDictionary<string, string> _propertyOverrides;
        private readonly IDotNetCliService _dotNetCli;
        private readonly SdksPathResolver _sdksPathResolver;
        private readonly MetadataFileReferenceCache _metadataFileReferenceCache;
        private readonly IEventEmitter _eventEmitter;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly FileSystemHelper _fileSystemHelper;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly ImmutableArray<IMSBuildEventSink> _eventSinks;

        private readonly object _gate = new object();
        private readonly Queue<ProjectFileInfo> _projectsToProcess;

        private PackageDependencyChecker _packageDependencyChecker;
        private ProjectManager _manager;
        private ProjectLoader _loader;
        private MSBuildOptions _options;
        private string _solutionFileOrRootPath;

        public string Key { get; } = "MsBuild";
        public string Language { get; } = LanguageNames.CSharp;
        public IEnumerable<string> Extensions { get; } = new[] { ".cs" };
        public bool EnabledByDefault { get; } = true;

        [ImportingConstructor]
        public ProjectSystem(
            IOmniSharpEnvironment environment,
            OmniSharpWorkspace workspace,
            IMSBuildLocator msbuildLocator,
            IDotNetCliService dotNetCliService,
            SdksPathResolver sdksPathResolver,
            MetadataFileReferenceCache metadataFileReferenceCache,
            IEventEmitter eventEmitter,
            IFileSystemWatcher fileSystemWatcher,
            FileSystemHelper fileSystemHelper,
            ILoggerFactory loggerFactory,
            [ImportMany] IEnumerable<IMSBuildEventSink> eventSinks)
        {
            _environment = environment;
            _workspace = workspace;
            _propertyOverrides = msbuildLocator.RegisteredInstance.PropertyOverrides;
            _dotNetCli = dotNetCliService;
            _sdksPathResolver = sdksPathResolver;
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _eventEmitter = eventEmitter;
            _fileSystemWatcher = fileSystemWatcher;
            _fileSystemHelper = fileSystemHelper;
            _loggerFactory = loggerFactory;
            _eventSinks = eventSinks.ToImmutableArray();

            _projectsToProcess = new Queue<ProjectFileInfo>();
            _logger = loggerFactory.CreateLogger<ProjectSystem>();
        }

        public void Initalize(IConfiguration configuration)
        {
            _options = new MSBuildOptions();
            ConfigurationBinder.Bind(configuration, _options);

            _sdksPathResolver.Enabled = _options.UseLegacySdkResolver;
            _sdksPathResolver.OverridePath = _options.MSBuildSDKsPath;

            if (_environment.LogLevel < LogLevel.Information)
            {
                var buildEnvironmentInfo = MSBuildHelpers.GetBuildEnvironmentInfo();
                _logger.LogDebug($"MSBuild environment: {Environment.NewLine}{buildEnvironmentInfo}");
            }

            _packageDependencyChecker = new PackageDependencyChecker(_loggerFactory, _eventEmitter, _dotNetCli, _options);
            _loader = new ProjectLoader(_options, _environment.TargetDirectory, _propertyOverrides, _loggerFactory, _sdksPathResolver);
            _manager = new ProjectManager(_loggerFactory, _options, _eventEmitter, _fileSystemWatcher, _metadataFileReferenceCache, _packageDependencyChecker, 
                _loader, _workspace, _eventSinks);

            if (_options.LoadProjectsOnDemand)
            {
                _logger.LogInformation($"Skip loading projects listed in solution file or under target directory because {Key}:{nameof(MSBuildOptions.LoadProjectsOnDemand)} is true.");
                return;
            }

            var initialProjectPaths = GetInitialProjectPaths();

            foreach (var projectFilePath in initialProjectPaths)
            {
                if (!File.Exists(projectFilePath))
                {
                    _logger.LogWarning($"Found project that doesn't exist on disk: {projectFilePath}");
                    continue;
                }

                _manager.QueueProjectUpdate(projectFilePath, allowAutoRestore: true);
            }
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
            return _fileSystemHelper.GetFiles("**/*.csproj");
        }

        private IEnumerable<string> GetProjectPathsFromSolution(string solutionFilePath)
        {
            _logger.LogInformation($"Detecting projects in '{solutionFilePath}'.");

            var solutionFile = SolutionFile.ParseFile(solutionFilePath);
            var processedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var project in solutionFile.Projects)
            {
                if (project.IsNotSupported)
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

        private static string FindSolutionFilePath(string rootPath, ILogger logger)
        {
            // currently, Directory.GetFiles collects files that the file extension has 'sln' prefix.
            // this causes collecting unexpected files like 'x.slnx', or 'x.slnproj'.
            // see https://docs.microsoft.com/en-us/dotnet/api/system.io.directory.getfiles?view=netframework-4.7.2 ('Note' description)
            var solutionsFilePaths = Directory.GetFiles(rootPath, "*.sln").Where(x => Path.GetExtension(x).Equals(".sln", StringComparison.OrdinalIgnoreCase)).ToArray();
            var result = SolutionSelector.Pick(solutionsFilePaths, rootPath);

            if (result.Message != null)
            {
                logger.LogInformation(result.Message);
            }

            return result.FilePath;
        }

        async Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            await _manager.WaitForQueueEmptyAsync();

            return new MSBuildWorkspaceInfo(
                _solutionFileOrRootPath, _manager.GetAllProjects(),
                excludeSourceFiles: request?.ExcludeSourceFiles ?? false);
        }

        async Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            await _manager.WaitForQueueEmptyAsync();

            var document = _workspace.GetDocument(filePath);

            var projectFilePath = document != null
                ? document.Project.FilePath
                : filePath;

            if (!_manager.TryGetProject(projectFilePath, out var projectFileInfo))
            {
                _logger.LogDebug($"Could not locate project for '{projectFilePath}'");
                return Task.FromResult<object>(null);
            }

            return new MSBuildProjectInfo(projectFileInfo);
        }
    }
}
