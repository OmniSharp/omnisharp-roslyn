using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("typelookup")]
        public async Task<IActionResult> TypeLookup(TypeLookupRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var response = new TypeLookupResponse();
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                if (symbol != null)
                {
                    if(symbol.Kind == SymbolKind.NamedType)
                    {
                        response.Type = symbol.ContainingNamespace.ToDisplayString() + "." 
                                        + symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    }
                    else
                    {
                        response.Type = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    }

                    if (request.IncludeDocumentation)
                    {
                        response.Documentation = symbol.GetDocumentationCommentXml();
                    }
                }
            }
            return new ObjectResult(response);
        }
    }
}