using System;
using System.Collections.Generic;
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
        public async Task<RenameResponse> Rename(RenameRequest request)
        {
            var response = new RenameResponse();

            var document = _workspace.GetDocument(request.FileName);
            if (document != null)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));

                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position);
                Solution solution = _workspace.CurrentSolution;

                if (symbol != null)
                {
                    try
                    {
                        solution = await Renamer.RenameSymbolAsync(solution, symbol, request.RenameTo, _workspace.Options);
                    }
                    catch (ArgumentException e)
                    {
                        response.ErrorMessage = e.Message;
                    }
                }

                var changes = new List<ModifiedFileResponse>();

                var solutionChanges = solution.GetChanges(_workspace.CurrentSolution);

                foreach (var projectChange in solutionChanges.GetProjectChanges())
                {
                    foreach (var changedDocumentId in projectChange.GetChangedDocuments())
                    {
                        var changedDocument = solution.GetDocument(changedDocumentId);
                        var modifiedFileResponse = new ModifiedFileResponse(changedDocument.FilePath);

                        if (!request.WantsTextChanges)
                        {
                            var changedText = await changedDocument.GetTextAsync();
                            modifiedFileResponse.Buffer = changedText.ToString();
                        }
                        else
                        {
                            var originalDocument = _workspace.CurrentSolution.GetDocument(changedDocumentId);
                            var textChanges = await changedDocument.GetTextChangesAsync(originalDocument);
                            modifiedFileResponse.Changes = await LinePositionSpanTextChange.Convert(originalDocument, textChanges);
                        }
                        
                        changes.Add(modifiedFileResponse);
                    }
                }

                // Attempt to update the workspace
                if (_workspace.TryApplyChanges(solution))
                {
                    response.Changes = changes;
                }
            }

            return response;
        }
    }
}