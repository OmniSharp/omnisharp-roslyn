using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Directives;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.v1;
using OmniSharp.Razor.Models;
using OmniSharp.Services;

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

        [ImportingConstructor]
        public RazorProjectSystem(IOmnisharpEnvironment environment,
                                   // OmnisharpWorkspace omnisharpWorkspace,
                                   IMetadataFileReferenceCache metadataFileReferenceCache,
                                   ILoggerFactory loggerFactory,
                                   IFileSystemWatcher watcher,
                                   IEventEmitter emitter)
        {
            _environment = environment;
            // _omnisharpWorkspace = omnisharpWorkspace;
            _logger = loggerFactory.CreateLogger<RazorProjectSystem>();
            _emitter = emitter;
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _watcher = watcher;
        }

        public IEnumerable<string> Extensions { get; } = new string[] { ".cshtml" };

        public string Key => RazorLanguage.Razor;

        public string Language => LanguageNames.CSharp;

        public void Initalize(IConfiguration configuration)
        {
            // This is pretty naive at the moment, and assumes we're in the mvc context.
            // This should be extended somehow in the future to boot up other hosts that are found
            // (perhaps dynamically?)
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(new RazorWorkspaceInformation());
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            _logger.LogDebug($"GetProjectModel: {filePath}");

//            var document = _omnisharpWorkspace.GetDocument(filePath);
//
//            var projectFilePath = document != null
//                ? document.Project.FilePath
//                : filePath;
//
//            var projectEntry = _projectStates.GetEntry(projectFilePath);
//            if (projectEntry == null)
//            {
//                return Task.FromResult<object>(null);
//            }

            return Task.FromResult<object>(new RazorProjectInformation());
        }
    }
}
