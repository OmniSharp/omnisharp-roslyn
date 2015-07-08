using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Roslyn;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("gotodefinition")]
        public async Task<GotoDefinitionResponse> GotoDefinition(GotoDefinitionRequest request)
        {
            var quickFixes = new List<QuickFix>();

            var document = _workspace.GetDocument(request.FileName);
            var response = new GotoDefinitionResponse();
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var syntaxTree = semanticModel.SyntaxTree;
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);

                if (symbol != null)
                {
                    var location = symbol.Locations.First();

                    if (location.IsInSource)
                    {
                        var lineSpan = symbol.Locations.First().GetMappedLineSpan();
                        response = new GotoDefinitionResponse
                        {
                            FileName = lineSpan.Path,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1
                        };
                    }
#if DNX451
                    else if (location.IsInMetadata)
                    {
                        var metadataDocument = await MetadataHelper.GetDocumentFromMetadata(document.Project, symbol);
                        var metadataLocation = await MetadataHelper.GetSymbolLocationFromMetadata(symbol, metadataDocument);
                        var lineSpan = metadataLocation.GetMappedLineSpan();

                        //TODO: Find the location of the symbol in the source
                        response = new GotoDefinitionResponse
                        {
                            FileName = lineSpan.Path,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1,
                            //MetadataSource = metadataSource.ToString()
                        };

                        if (request.WantMetadataSource)
                        {
                            var metadataSource = await metadataDocument.GetTextAsync();
                            response.MetadataSource = metadataSource.ToString();
                        }
                    }
#endif
                }
            }

            return response;
        }
    }
}
