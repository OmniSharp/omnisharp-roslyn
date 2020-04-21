using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dotnet.Script.DependencyModel.NuGet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.FileSystem;
using OmniSharp.FileWatching;
using OmniSharp.Mef;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Services;

namespace OmniSharp.Script
{
    [ExportProjectSystem(ProjectSystemNames.ScriptProjectSystem), Shared]
    public class ScriptProjectSystem : IProjectSystem
    {
        private const string CsxExtension = ".csx";
        private readonly ConcurrentDictionary<string, ProjectInfo> _projects = new ConcurrentDictionary<string, ProjectInfo>();
        private readonly ScriptContextProvider _scriptContextProvider;
        private readonly OmniSharpWorkspace _workspace;
        private readonly IOmniSharpEnvironment _env;
        private readonly ILogger _logger;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly FileSystemHelper _fileSystemHelper;
        private ScriptOptions _scriptOptions;
        private Lazy<ScriptContext> _scriptContext;

        [ImportingConstructor]
        public ScriptProjectSystem(OmniSharpWorkspace workspace, IOmniSharpEnvironment env, ILoggerFactory loggerFactory,
            ScriptContextProvider scriptContextProvider, IFileSystemWatcher fileSystemWatcher, FileSystemHelper fileSystemHelper)
        {
            _workspace = workspace;
            _env = env;
            _fileSystemWatcher = fileSystemWatcher;
            _fileSystemHelper = fileSystemHelper;
            _logger = loggerFactory.CreateLogger<ScriptProjectSystem>();
            _scriptContextProvider = scriptContextProvider;
        }

        public string Key { get; } = "Script";
        public string Language { get; } = LanguageNames.CSharp;
        public IEnumerable<string> Extensions { get; } = new[] { CsxExtension };
        public bool EnabledByDefault { get; } = true;
        public bool Initialized { get; private set; }

        public void Initalize(IConfiguration configuration)
        {
            if (Initialized) return;

            _scriptOptions = new ScriptOptions();

            ConfigurationBinder.Bind(configuration, _scriptOptions);

            _logger.LogInformation($"Detecting CSX files in '{_env.TargetDirectory}'.");

            // Nothing to do if there are no CSX files
            var allCsxFiles = _fileSystemHelper.GetFiles("**/*.csx").ToArray();

            _scriptContext = new Lazy<ScriptContext>(() => _scriptContextProvider.CreateScriptContext(_scriptOptions, allCsxFiles, _workspace.EditorConfigEnabled));

            if (allCsxFiles.Length == 0)
            {
                _logger.LogInformation("Could not find any CSX files");
                Initialized = true;

                // Watch CSX files in order to add/remove them in workspace
                _fileSystemWatcher.Watch(CsxExtension, OnCsxFileChanged);
                return;
            }

            _logger.LogInformation($"Found {allCsxFiles.Length} CSX files.");

            // Each .CSX file becomes an entry point for its own project
            // Every #loaded file will be part of the project too
            foreach (var csxPath in allCsxFiles)
            {
                AddToWorkspace(csxPath);
            }

            // Watch CSX files in order to add/remove them in workspace
            _fileSystemWatcher.Watch(CsxExtension, OnCsxFileChanged);

            Initialized = true;
        }

        private void OnCsxFileChanged(string filePath, FileChangeType changeType)
        {
            if (changeType == FileChangeType.Unspecified && !File.Exists(filePath) ||
                changeType == FileChangeType.Delete)
            {
                RemoveFromWorkspace(filePath);
            }

            if (changeType == FileChangeType.Unspecified && File.Exists(filePath) ||
                changeType == FileChangeType.Create)
            {
                AddToWorkspace(filePath);
            }
        }

        private void AddToWorkspace(string csxPath)
        {
            try
            {
                var csxFileName = Path.GetFileName(csxPath);
                var project = _scriptContext.Value.ScriptProjectProvider.CreateProject(csxFileName, _scriptContext.Value.MetadataReferences, csxPath, _scriptContext.Value.GlobalsType);

                if (_scriptOptions.IsNugetEnabled())
                {
                    var scriptMap = _scriptContext.Value.CompilationDependencies.ToDictionary(rdt => rdt.Name, rdt => rdt.Scripts);
                    var options = project.CompilationOptions.WithSourceReferenceResolver(
                        new NuGetSourceReferenceResolver(ScriptSourceResolver.Default,
                            scriptMap));
                    project = project.WithCompilationOptions(options);
                }

                // add CSX project to workspace
                _workspace.AddProject(project);
                _workspace.AddDocument(project.Id, csxPath, SourceCodeKind.Script);
                _projects[csxPath] = project;
                _logger.LogInformation($"Added CSX project '{csxPath}' to the workspace.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{csxPath} will be ignored due to an following error");
            }
        }

        private void RemoveFromWorkspace(string csxPath)
        {
            if (_projects.TryRemove(csxPath, out var project))
            {
                _workspace.RemoveProject(project.Id);
                _logger.LogInformation($"Removed CSX project '{csxPath}' from the workspace.");
            }
        }

        private ProjectInfo GetProjectFileInfo(string path)
        {
            if (!_projects.TryGetValue(path, out ProjectInfo projectFileInfo))
            {
                return null;
            }

            return projectFileInfo;
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            // only react to .CSX file paths
            if (!filePath.EndsWith(CsxExtension, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<object>(null);
            }

            var document = _workspace.GetDocument(filePath);
            var projectFilePath = document != null
                ? document.Project.FilePath
                : filePath;

            var projectInfo = GetProjectFileInfo(projectFilePath);
            if (projectInfo == null)
            {
                _logger.LogDebug($"Could not locate project for '{projectFilePath}'");
                return Task.FromResult<object>(null);
            }

            return Task.FromResult<object>(new ScriptContextModel(filePath, projectInfo));
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            var scriptContextModels = new List<ScriptContextModel>();
            foreach (var project in _projects)
            {
                scriptContextModels.Add(new ScriptContextModel(project.Key, project.Value));
            }
            return Task.FromResult<object>(new ScriptContextModelCollection(scriptContextModels));
        }
    }
}
