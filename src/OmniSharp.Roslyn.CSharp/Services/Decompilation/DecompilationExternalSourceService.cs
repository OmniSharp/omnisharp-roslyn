using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using System;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.Decompilation
{
    [Export(typeof(DecompilationExternalSourceService)), Shared]
    public class DecompilationExternalSourceService : BaseExternalSourceService, IExternalSourceService
    {
        private const string DecompiledKey = "$Decompiled$";
        private readonly ILoggerFactory _loggerFactory;
        private readonly Lazy<OmniSharpCSharpDecompiledSourceService> _service;

        [ImportingConstructor]
        public DecompilationExternalSourceService(ILoggerFactory loggerFactory) : base()
        {
            _loggerFactory = loggerFactory;
            _service = new Lazy<OmniSharpCSharpDecompiledSourceService>(() => new OmniSharpCSharpDecompiledSourceService(_loggerFactory));
        }

        public async Task<(Document document, string documentPath)> GetAndAddExternalSymbolDocument(Project project, ISymbol symbol, CancellationToken cancellationToken)
        {
            var fileName = symbol.GetFilePathForExternalSymbol(project);

            Project decompilationProject;

            // since submission projects cannot have new documents added to it
            // we will use a separate project to hold decompiled documents
            if (project.IsSubmission)
            {
                decompilationProject = project.Solution.Projects.FirstOrDefault(x => x.Name == DecompiledKey);
                if (decompilationProject == null)
                {
                    decompilationProject = project.Solution.AddProject(DecompiledKey, $"{DecompiledKey}.dll", LanguageNames.CSharp)
                        .WithCompilationOptions(project.CompilationOptions)
                        .WithMetadataReferences(project.MetadataReferences);
                }
            }
            else
            {
                // for regular projects we will use current project to store decompiled docs
                decompilationProject = project;
            }

            if (!_cache.TryGetValue(fileName, out var document))
            {
                var topLevelSymbol = symbol.GetTopLevelContainingNamedType();
                var temporaryDocument = decompilationProject.AddDocument(fileName, string.Empty);

                var compilation = await decompilationProject.GetCompilationAsync();
                document = await _service.Value.AddSourceToAsync(temporaryDocument, compilation, topLevelSymbol, cancellationToken);

                _cache.TryAdd(fileName, document);
            }

            return (document, fileName);
        }
    }
}
