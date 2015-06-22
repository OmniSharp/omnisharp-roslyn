using System;
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
        public async Task<RenameResponse> Rename(RenameRequest request)
        {
            var response = new RenameResponse();

            var document = _workspace.GetDocument(request.FileName);
            if (document != null)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));

                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position);
                var solution = _workspace.CurrentSolution;

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
                var changes = await FileChanges.GetFileChangesAsync(solution, _workspace.CurrentSolution, null, request.WantsTextChanges);

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
