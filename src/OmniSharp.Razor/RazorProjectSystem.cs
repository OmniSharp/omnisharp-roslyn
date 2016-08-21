using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Directives;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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
        private readonly IOmnisharpEnvironment _environment;
        private readonly OmnisharpWorkspace _omnisharpWorkspace;
        private readonly RazorWorkspace _razorWorkspace;

        [ImportingConstructor]
        public RazorProjectSystem(IOmnisharpEnvironment environment,
                                   OmnisharpWorkspace omnisharpWorkspace,
                                   RazorWorkspace razorWorkspace,
                                   ILoggerFactory loggerFactory,
                                   IEventEmitter emitter)
        {
            _environment = environment;
            _omnisharpWorkspace = omnisharpWorkspace;
            _razorWorkspace = razorWorkspace;
            _logger = loggerFactory.CreateLogger<RazorProjectSystem>();
            _emitter = emitter;
        }

        public IEnumerable<string> Extensions { get; } = new string[] { ".cshtml" };

        public string Key => RazorLanguage.Razor;

        public string Language => LanguageNames.CSharp;

        public void Initalize(IConfiguration configuration)
        {
            // This is pretty naive at the moment, and assumes we're in the mvc context.
            // This should be extended somehow in the future to boot up other hosts that are found
            // (perhaps dynamically?)

            // TODO: Create subprojects for each mvc project containing Razor files.
            UpdateSourceFiles(FindAllRazorFiles());
        }


        private void UpdateSourceFiles(IEnumerable<string> sourceFiles)
        {
            var id = ProjectId.CreateNewId();
            var info = ProjectInfo.Create(
                id: id,
                version: VersionStamp.Create(),
                name: $"Razor",
                assemblyName: "Razor",
                language: RazorLanguage.Razor);

            _omnisharpWorkspace.AddProject(info);

            _logger.LogInformation($"Added Razor project");

            var existingFiles = info.Documents.Select(x => x.FilePath).ToImmutableHashSet();

            sourceFiles = sourceFiles.Where(filename => Extensions.Contains(Path.GetExtension(filename)));

            var added = 0;
            var removed = 0;

            foreach (var file in sourceFiles)
            {
                if (existingFiles.Contains(file))
                {
                    existingFiles = existingFiles.Remove(file);
                    continue;
                }

                // TODO: performance optimize
                using (var stream = File.OpenRead(file))
                {
                    // TODO: other encoding option?
                    var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);
                    var docId = DocumentId.CreateNewId(id);
                    var version = VersionStamp.Create();

                    var loader = TextLoader.From(TextAndVersion.Create(sourceText, version));

                    var doc = DocumentInfo.Create(docId, file, filePath: file, loader: loader);
                    _omnisharpWorkspace.AddDocument(doc);

                    _logger.LogDebug($"    Added document {file}.");
                    added++;
                }
            }

            foreach (var document in existingFiles.Select(x => info.Documents.First(z => z.FilePath == x)))
            {
                _omnisharpWorkspace.RemoveDocument(document.Id);
                _logger.LogDebug($"    Removed document {document.FilePath}.");
                removed++;
            }

            if (added != 0 || removed != 0)
            {
                _logger.LogInformation($"    Added {added} and removed {removed} documents.");
            }
        }

        public IEnumerable<string> FindAllRazorFiles()
        {
            return Directory.EnumerateFiles(_environment.Path, "*.cshtml", SearchOption.AllDirectories);
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(new RazorWorkspaceInformation());
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            _logger.LogDebug($"GetProjectModel: {filePath}");

            return Task.FromResult<object>(new RazorProjectInformation());
        }
    }
}
