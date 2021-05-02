using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.GotoDefinition;
using OmniSharp.Models.Metadata;
using OmniSharp.Options;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.GotoDefinition, LanguageNames.CSharp)]
    public class GotoDefinitionService : IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>
    {
        private readonly OmniSharpOptions _omnisharpOptions;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ExternalSourceServiceFactory _externalSourceServiceFactory;

        [ImportingConstructor]
        public GotoDefinitionService(OmniSharpWorkspace workspace, ExternalSourceServiceFactory externalSourceServiceFactory, OmniSharpOptions omnisharpOptions)
        {
            _workspace = workspace;
            _externalSourceServiceFactory = externalSourceServiceFactory;
            _omnisharpOptions = omnisharpOptions;
        }

        public async Task<GotoDefinitionResponse> Handle(GotoDefinitionRequest request)
        {
            var externalSourceService = _externalSourceServiceFactory.Create(_omnisharpOptions);
            var document = externalSourceService.FindDocumentInCache(request.FileName) ??
                _workspace.GetDocument(request.FileName);

            var response = new GotoDefinitionResponse();

            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.GetTextPosition(request);
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);

                // go to definition for namespaces is not supported
                if (symbol != null && !(symbol is INamespaceSymbol))
                {
                    // for partial methods, pick the one with body
                    if (symbol is IMethodSymbol method)
                    {
                        // Return an empty response for property accessor symbols like get and set
                        if (method.AssociatedSymbol is IPropertySymbol)
                            return response;

                        symbol = method.PartialImplementationPart ?? symbol;
                    }

                    var location = symbol.Locations.First();

                    if (location.IsInSource)
                    {
                        var lineSpan = symbol.Locations.First().GetMappedLineSpan();
                        response = new GotoDefinitionResponse
                        {
                            FileName = lineSpan.Path,
                            Line = lineSpan.StartLinePosition.Line,
                            Column = lineSpan.StartLinePosition.Character
                        };
                    }
                    else if (location.IsInMetadata && request.WantMetadata)
                    {
                        var cancellationToken = _externalSourceServiceFactory.CreateCancellationToken(_omnisharpOptions, request.Timeout);
                        var (metadataDocument, _) = await externalSourceService.GetAndAddExternalSymbolDocument(document.Project, symbol, cancellationToken);
                        if (metadataDocument != null)
                        {
                            cancellationToken = _externalSourceServiceFactory.CreateCancellationToken(_omnisharpOptions, request.Timeout);
                            var metadataLocation = await externalSourceService.GetExternalSymbolLocation(symbol, metadataDocument, cancellationToken);
                            var lineSpan = metadataLocation.GetMappedLineSpan();

                            response = new GotoDefinitionResponse
                            {
                                Line = lineSpan.StartLinePosition.Line,
                                Column = lineSpan.StartLinePosition.Character,
                                MetadataSource = new MetadataSource()
                                {
                                    AssemblyName = symbol.ContainingAssembly.Name,
                                    ProjectName = document.Project.Name,
                                    TypeName = symbol.GetSymbolName()
                                },
                            };
                        }
                    }
                }
            }

            return response;
        }
    }
}
