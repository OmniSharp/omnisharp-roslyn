using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.GotoDefinition;
using OmniSharp.Models.Metadata;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Decompilation;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.GotoDefinition, LanguageNames.CSharp)]
    public class GotoDefinitionService : IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>
    {
        private readonly MetadataHelper _metadataHelper;
        private readonly DecompilationHelper _decompilationHelper;
        private readonly OmniSharpOptions _omnisharpOptions;
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public GotoDefinitionService(OmniSharpWorkspace workspace, MetadataHelper metadataHelper, DecompilationHelper decompilationHelper, OmniSharpOptions omnisharpOptions)
        {
            _workspace = workspace;
            _metadataHelper = metadataHelper;
            _decompilationHelper = decompilationHelper;
            _omnisharpOptions = omnisharpOptions;
        }

        public async Task<GotoDefinitionResponse> Handle(GotoDefinitionRequest request)
        {
            var document = _metadataHelper.FindDocumentInMetadataCache(request.FileName) ??
                _workspace.GetDocument(request.FileName);

            var response = new GotoDefinitionResponse();

            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
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
                        var metadataLocation = await GetMetadataLocation(document, symbol, request.Timeout);
                        var lineSpan = metadataLocation.GetMappedLineSpan();

                        response = new GotoDefinitionResponse
                        {
                            Line = lineSpan.StartLinePosition.Line,
                            Column = lineSpan.StartLinePosition.Character,
                            MetadataSource = new MetadataSource()
                            {
                                AssemblyName = symbol.ContainingAssembly.Name,
                                ProjectName = document.Project.Name,
                                TypeName = _metadataHelper.GetSymbolName(symbol)
                            },
                        };
                    }
                }
            }

            return response;
        }

        private async Task<Location> GetMetadataLocation(Document document, ISymbol symbol, int timeout)
        {
            var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));

            if (_omnisharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport)
            {
                var (metadataDoc, _) = await _decompilationHelper.GetAndAddDecompiledDocument(document.Project, symbol, cancellationSource.Token);
                return Location.None;
            }

            var (metadataDocument, _) = await _metadataHelper.GetAndAddDocumentFromMetadata(document.Project, symbol, cancellationSource.Token);
            if (metadataDocument != null)
            {
                cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));

                var metadataLocation = await _metadataHelper.GetSymbolLocationFromMetadata(symbol, metadataDocument, cancellationSource.Token);
                return metadataLocation;
            }

            return Location.None;
        }
    }
}
