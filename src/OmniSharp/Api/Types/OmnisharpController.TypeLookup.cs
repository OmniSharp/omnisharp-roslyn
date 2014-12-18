using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using System.Threading.Tasks;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("typelookup")]
        public async Task<IActionResult> TypeLookup([FromBody]TypeLookupRequest request)
        {
            _workspace.EnsureBufferUpdated(request);

            var documentId = _workspace.GetDocumentId(request.FileName);
            var response = new TypeLookupResponse();
            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                if (symbol != null)
                {
                    response.Type = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

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