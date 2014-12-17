using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("gotodefinition")]
        public async Task<IActionResult> GotoDefinition([FromBody]Request request)
        {
            _workspace.EnsureBufferUpdated(request);

            var quickFixes = new List<QuickFix>();

            var documentId = _workspace.GetDocumentId(request.FileName);
            var response = new GotoDefinitionResponse();
            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var semanticModel = await document.GetSemanticModelAsync();
                var syntaxTree = semanticModel.SyntaxTree;
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                var lineSpan = symbol.Locations.First().GetMappedLineSpan();
                response = new GotoDefinitionResponse
                {
                    FileName = lineSpan.Path,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1
                };
            }

            return new ObjectResult(response);
        }
    }
}