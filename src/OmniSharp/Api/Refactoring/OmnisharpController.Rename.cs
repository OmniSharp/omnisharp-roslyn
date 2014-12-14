using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("rename")]
        public async Task<IActionResult> Rename([FromBody]RenameRequest request)
        {
            EnsureBufferUpdated(request);

            var response = new RenameResponse();

            var documentIds = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName);

            var documentId = documentIds.FirstOrDefault();
            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column));

                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position);
                Solution solution = _workspace.CurrentSolution;

                if (symbol != null)
                {
                    solution = await Renamer.RenameSymbolAsync(solution, symbol, request.RenameTo, _workspace.Options);
                }

                var changes = new List<ModifiedFileResponse>();

                var solutionChanges = solution.GetChanges(_workspace.CurrentSolution);

                foreach (var projectChange in solutionChanges.GetProjectChanges())
                {
                    foreach (var changedDocumentId in projectChange.GetChangedDocuments())
                    {
                        var changedDocument = solution.GetDocument(changedDocumentId);
                        var changedText = await changedDocument.GetTextAsync();
                        var modifiedFileResponse = new ModifiedFileResponse(changedDocument.FilePath, changedText.ToString());

                        changes.Add(modifiedFileResponse);
                    }
                }

                // Attempt to update the workspace
                if (_workspace.TryApplyChanges(solution))
                {
                    response.Changes = changes;
                }
            }

            return new ObjectResult(response);
        }
    }
}