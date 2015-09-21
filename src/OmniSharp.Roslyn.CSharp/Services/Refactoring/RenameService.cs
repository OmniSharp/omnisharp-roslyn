using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(typeof(RequestHandler<RenameRequest, RenameResponse>), LanguageNames.CSharp)]
    public class RenameService : RequestHandler<RenameRequest, RenameResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public RenameService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<RenameResponse> Handle(RenameRequest request)
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

                var changes = new Dictionary<string, ModifiedFileResponse>();
                var solutionChanges = solution.GetChanges(_workspace.CurrentSolution);

                foreach (var projectChange in solutionChanges.GetProjectChanges())
                {
                    foreach (var changedDocumentId in projectChange.GetChangedDocuments())
                    {
                        var changedDocument = solution.GetDocument(changedDocumentId);

                        ModifiedFileResponse modifiedFileResponse;
                        if (!changes.TryGetValue(changedDocument.FilePath, out modifiedFileResponse))
                        {
                            modifiedFileResponse = new ModifiedFileResponse(changedDocument.FilePath);
                            changes[changedDocument.FilePath] = modifiedFileResponse;
                        }

                        if (!request.WantsTextChanges)
                        {
                            var changedText = await changedDocument.GetTextAsync();
                            modifiedFileResponse.Buffer = changedText.ToString();
                        }
                        else
                        {
                            var originalDocument = _workspace.CurrentSolution.GetDocument(changedDocumentId);
                            var textChanges = await changedDocument.GetTextChangesAsync(originalDocument);
                            var linePositionSpanTextChanges = await LinePositionSpanTextChange.Convert(originalDocument, textChanges);

                            modifiedFileResponse.Changes = modifiedFileResponse.Changes != null
                                ? modifiedFileResponse.Changes.Union(linePositionSpanTextChanges)
                                : linePositionSpanTextChanges;
                        }
                    }
                }

                if (request.ApplyTextChanges)
                {
                    // Attempt to update the workspace
                    if (_workspace.TryApplyChanges(solution))
                    {
                        response.Changes = changes.Values;
                    }
                }
                else
                {
                    response.Changes = changes.Values;
                }
            }

            return response;
        }
    }
}
