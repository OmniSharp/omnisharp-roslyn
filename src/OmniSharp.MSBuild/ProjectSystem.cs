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
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
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
        private readonly IAnalyzerAssemblyLoader _assemblyLoader;
        private readonly DotNetInfo _dotNetInfo;
        private readonly ImmutableArray<IMSBuildEventSink> _eventSinks;
        private PackageDependencyChecker _packageDependencyChecker;
        private ProjectManager _manager;
        private ProjectLoader _loader;
        private MSBuildOptions _options;
        private string _solutionFileOrRootPath;
        public string Key { get; } = "MsBuild";
        public string Language { get; } = LanguageNames.CSharp;
        public IEnumerable<string> Extensions { get; } = new[] { ".cs" };
        public bool EnabledByDefault { get; } = true;
        public bool Initialized { get; private set; }

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
            CachingCodeFixProviderForProjects codeFixesForProjects,
            IAnalyzerAssemblyLoader assemblyLoader,
            [ImportMany] IEnumerable<IMSBuildEventSink> eventSinks,
            DotNetInfo dotNetInfo)
        {
            _environment = environment;
            _workspace = workspace;
            _propertyOverrides = msbuildLocator.RegisteredInstance?.PropertyOverrides ?? ImmutableDictionary.Create<string, string>();
            _dotNetCli = dotNetCliService;
            _sdksPathResolver = sdksPathResolver;
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _eventEmitter = eventEmitter;
            _fileSystemWatcher = fileSystemWatcher;
            _fileSystemHelper = fileSystemHelper;
            _loggerFactory = loggerFactory;
            _eventSinks = eventSinks.ToImmutableArray();
            _logger = loggerFactory.CreateLogger<ProjectSystem>();
            _assemblyLoader = assemblyLoader;
            _dotNetInfo = dotNetInfo;
        }

        public void Initalize(IConfiguration configuration)
        {
            if (Initialized) return;

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

            _manager = new ProjectManager(_loggerFactory, _options, _eventEmitter, _fileSystemWatcher, _metadataFileReferenceCache, _packageDependencyChecker, _loader, _workspace, _assemblyLoader, _eventSinks, _dotNetInfo);
            Initialized = true;

            if (_options.LoadProjectsOnDemand)
            {
                _logger.LogInformation($"Skip loading projects listed in solution file or under target directory because {Key}:{nameof(MSBuildOptions.LoadProjectsOnDemand)} is true.");
                return;
            }

            var initialProjectPathsAndIds = GetInitialProjectPathsAndIds();

            foreach (var (projectFilePath, projectIdInfo) in initialProjectPathsAndIds)
            {
                if (!File.Exists(projectFilePath))
                {
                    _logger.LogWarning($"Found project that doesn't exist on disk: {projectFilePath}");
                    continue;
                }

                _manager.QueueProjectUpdate(projectFilePath, allowAutoRestore: true, projectIdInfo);
            }
        }

        public Task WaitForIdleAsync() { return _manager.WaitForQueueEmptyAsync(); }

        private IEnumerable<(string, ProjectIdInfo)> GetInitialProjectPathsAndIds()
        {
            // If a solution was provided, use it.
            if (!string.IsNullOrEmpty(_environment.SolutionFilePath))
            {
                _solutionFileOrRootPath = _environment.SolutionFilePath;
                return GetProjectPathsAndIdsFromSolutionOrFilter(_environment.SolutionFilePath);
            }

            // Otherwise, assume that the path provided is a directory and look for a solution there.
            var solutionFilePath = FindSolutionFilePath(_environment.TargetDirectory, _logger);
            if (!string.IsNullOrEmpty(solutionFilePath))
            {
                _solutionFileOrRootPath = solutionFilePath;
                return GetProjectPathsAndIdsFromSolutionOrFilter(solutionFilePath);
            }

            // Finally, if there isn't a single solution immediately available,
            // Just process all of the projects beneath the root path.
            _solutionFileOrRootPath = _environment.TargetDirectory;
            return _fileSystemHelper.GetFiles("**/*.csproj")
                .Select(filepath =>
            {
                var projectIdInfo = new ProjectIdInfo(ProjectId.CreateNewId(debugName: filepath), isDefinedInSolution: false);
                return (filepath, projectIdInfo);
            });
        }

        private IEnumerable<(string, ProjectIdInfo)> GetProjectPathsAndIdsFromSolutionOrFilter(string solutionOrFilterFilePath)
        {
            _logger.LogInformation($"Detecting projects in '{solutionOrFilterFilePath}'.");

            var solutionFilePath = solutionOrFilterFilePath;

            var projectFilter = ImmutableHashSet<string>.Empty;
            if (SolutionFilterReader.IsSolutionFilterFilename(solutionOrFilterFilePath) &&
                !SolutionFilterReader.TryRead(solutionOrFilterFilePath, out solutionFilePath, out projectFilter))
            {
                throw new InvalidSolutionFileException("Solution filter file was invalid.");
            }

            var projects = ImmutableArray<SolutionFileProjectInfo>.Empty;
            if(!SolutionFileReader.TryRead(solutionFilePath, projectFilter, out projects))
            {
                throw new InvalidSolutionFileException("Solution file was invalid.");
            }

            var result = new List<(string, ProjectIdInfo)>();
            foreach (var project in projects)
            {
                var projectIdInfo = new ProjectIdInfo(ProjectId.CreateFromSerialized(new Guid(project.ProjectGuid)), true);

                var convertedConfigurations = new Dictionary<string, string>();
                foreach(var configuration in project.SolutionConfigurations)
                {
                    convertedConfigurations.Add(configuration, configuration);
                }
                projectIdInfo.SolutionConfiguration = convertedConfigurations;
                result.Add((project.ProjectName, projectIdInfo));
            }

            return result;
        }

        private static string FindSolutionFilePath(string rootPath, ILogger logger)
        {
            // Currently, Directory.GetFiles on Windows has a special case for a pattern
            // containing a wildcard followed by a 3-letter extension. This results in
            // inconsistency between Windows and Mono. Therefore avoid using this
            // Windows-specific behavior.
            // See https://docs.microsoft.com/en-us/dotnet/api/system.io.directory.getfiles?view=netframework-4.7.2 ('Note' description)
            var solutionsFilePaths = Directory.EnumerateFiles(rootPath).Where(x => {
                var extension = Path.GetExtension(x);
                return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
            }).ToArray();
            // Windows specific behaviour does not apply here, as slnf contains 4 letters
            var solutionFiltersFilePaths = Directory.GetFiles(rootPath, "*.slnf").ToArray();
            var result = SolutionSelector.Pick(solutionsFilePaths.Concat(solutionFiltersFilePaths).ToArray(), rootPath);

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
