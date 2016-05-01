using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.Services;
using OmniSharp.DotNet.Models;
using OmniSharp.Razor.Models;
using OmniSharp.Razor.Projects;

namespace OmniSharp.Razor
{
    [Export(typeof(IProjectSystem)), Shared]
    public class RazorProjectSystem : IProjectSystem
    {
        private readonly ILogger _logger;
        private readonly IEventEmitter _emitter;
        private readonly IFileSystemWatcher _watcher;
        private readonly IOmnisharpEnvironment _environment;
        private readonly IMetadataFileReferenceCache _metadataFileReferenceCache;
        private readonly string _compilationConfiguration = "Debug";
        private readonly OmnisharpWorkspace _omnisharpWorkspace;
        //private readonly ProjectStatesCache _projectStates;
        //private WorkspaceContext _workspaceContext;
        private bool _enableRestorePackages = false;

        [ImportingConstructor]
        public RazorProjectSystem(IOmnisharpEnvironment environment,
                                   OmnisharpWorkspace omnisharpWorkspace,
                                   IMetadataFileReferenceCache metadataFileReferenceCache,
                                   ILoggerFactory loggerFactory,
                                   IFileSystemWatcher watcher,
                                   IEventEmitter emitter)
        {
            _environment = environment;
            _omnisharpWorkspace = omnisharpWorkspace;
            _logger = loggerFactory.CreateLogger<RazorProjectSystem>();
            _emitter = emitter;
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _watcher = watcher;

            //_projectStates = new ProjectStatesCache(loggerFactory, _emitter);
        }

        public IEnumerable<string> Extensions { get; } = new string[] { ".cshtml" };

        public string Key => "Razor";

        public string Language => LanguageNames.CSharp;

        public Task<object> GetInformationModel(WorkspaceInformationRequest request)
        {
            var workspaceInfo = new RazorWorkspaceInformation(
                //entries: _projectStates.GetStates,
                includeSourceFiles: !request.ExcludeSourceFiles);

            return Task.FromResult<object>(workspaceInfo);
        }

        public Task<object> GetProjectModel(string path)
        {
            _logger.LogDebug($"GetProjectModel {path}");
            var document = _omnisharpWorkspace.GetDocument(path);
            if (document == null)
            {
                return Task.FromResult<object>(null);
            }

            var projectPath = document.Project.FilePath;
            _logger.LogDebug($"GetProjectModel {path}=>{projectPath}");
            //var projectEntry = _projectStates.GetOrAddEntry(projectPath);
            var projectInformation = new RazorProjectInformation(/*projectEntry*/);
            return Task.FromResult<object>(projectInformation);
        }

        public void Initalize(IConfiguration configuration)
        {
            _logger.LogInformation($"Initializing in {_environment.Path}");

            if (!bool.TryParse(configuration["enablePackageRestore"], out _enableRestorePackages))
            {
                _enableRestorePackages = false;
            }

            _logger.LogInformation($"Auto package restore: {_enableRestorePackages}");

            //_workspaceContext = WorkspaceContext.Create();
            var projects = ProjectSearcher.Search(_environment.Path);
            _logger.LogInformation($"Originated from {projects.Count()} projects.");
            foreach (var path in projects)
            {
                _workspaceContext.AddProject(path);
            }

            Update(allowRestore: true);
        }
    }
}
