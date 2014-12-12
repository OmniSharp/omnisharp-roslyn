using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    [Route("/")]
    public class OmnisharpController
    {
        private readonly OmnisharpWorkspace _workspace;

        public OmnisharpController(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        [HttpPost("autocomplete")]
        public async Task<IActionResult> AutoComplete([FromBody]Request request)
        {
            var completions = Enumerable.Empty<AutoCompleteResponse>();

            EnsureBufferUpdated(request);

            var documentIds = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName);

            var documentId = documentIds.FirstOrDefault();
            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column));
                var model = await document.GetSemanticModelAsync();
                var symbols = Recommender.GetRecommendedSymbolsAtPosition(model, position, _workspace);

                completions = symbols.Select(s => new AutoCompleteResponse { CompletionText = s.Name, DisplayText = s.Name });
            }
            else
            {
                return new HttpNotFoundResult();
            }

            return new ObjectResult(completions);
        }

        private void EnsureBufferUpdated(Request request)
        {
            foreach (var documentId in _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName))
            {
                var buffer = Encoding.UTF8.GetBytes(request.Buffer);
                var sourceText = SourceText.From(new MemoryStream(buffer), encoding: Encoding.UTF8);
                _workspace.OnDocumentChanged(documentId, sourceText);
            }
        }
    }
}