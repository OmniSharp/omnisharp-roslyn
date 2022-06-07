using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.Rename;
using OmniSharp.Options;
using OmniSharp.Roslyn.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.Rename, LanguageNames.CSharp)]
    public class RenameService : IRequestHandler<RenameRequest, RenameResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly OmniSharpOptions _omniSharpOptions;

        [ImportingConstructor]
        public RenameService(OmniSharpWorkspace workspace, OmniSharpOptions omniSharpOptions)
        {
            _workspace = workspace;
            _omniSharpOptions = omniSharpOptions;
        }

        public async Task<RenameResponse> Handle(RenameRequest request)
        {
            var response = new RenameResponse();

            var document = _workspace.GetDocument(request.FileName);
            if (document != null)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.GetTextPosition(request);

                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position);
                Solution solution = _workspace.CurrentSolution;

                if (symbol != null)
                {
                    var options = new OmniSharpRenameOptions(
                        RenameOverloads: _omniSharpOptions.RenameOptions.RenameOverloads,
                        RenameInStrings: _omniSharpOptions.RenameOptions.RenameInStrings,
                        RenameInComments: _omniSharpOptions.RenameOptions.RenameInComments);

                    (solution, response.ErrorMessage) = await OmniSharpRenamer.RenameSymbolAsync(solution, symbol, request.RenameTo, options, nonConflictSymbols: null, CancellationToken.None);

                    if (response.ErrorMessage is not null)
                    {
                        // An error occurred. There are no changes to report.
                        return response;
                    }
                }

                var changes = new Dictionary<string, ModifiedFileResponse>();
                var solutionChanges = solution.GetChanges(_workspace.CurrentSolution);

                foreach (var projectChange in solutionChanges.GetProjectChanges())
                {
                    foreach (var changedDocumentId in projectChange.GetChangedDocuments())
                    {
                        var changedDocument = solution.GetDocument(changedDocumentId);

                        if (!changes.TryGetValue(changedDocument.FilePath, out var modifiedFileResponse))
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
                            var linePositionSpanTextChanges = await TextChanges.GetAsync(changedDocument, originalDocument);

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
