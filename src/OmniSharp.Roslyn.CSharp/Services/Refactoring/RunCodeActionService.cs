using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmnisharpEndpoints.RunCodeAction, LanguageNames.CSharp)]
    public class RunCodeActionsService : RequestHandler<RunCodeActionRequest, RunCodeActionResponse>
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _codeActionProviders;

        [ImportingConstructor]
        public RunCodeActionsService(OmnisharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers)
        {
            _workspace = workspace;
            _codeActionProviders = providers;
        }

        public async Task<RunCodeActionResponse> Handle(RunCodeActionRequest request)
        {
            var actions = new List<CodeAction>();
            var context = await GetContext(request, actions);
            await GetContextualCodeActions(context);

            if (request.CodeAction > actions.Count())
            {
                return new RunCodeActionResponse();
            }

            var action = actions.ElementAt(request.CodeAction);

            var operations = await action.GetOperationsAsync(CancellationToken.None);

            foreach (var o in operations)
            {
                o.Apply(_workspace, CancellationToken.None);
            }

            var originalDocument = context.Value.Document;
            var response = new RunCodeActionResponse();
            if (!request.WantsTextChanges)
            {
                // return the new document
                var sourceText = await _workspace.CurrentSolution.GetDocument(originalDocument.Id).GetTextAsync();
                response.Text = sourceText.ToString();
            }
            else
            {
                // return the text changes
                var changes = await _workspace.CurrentSolution.GetDocument(originalDocument.Id).GetTextChangesAsync(originalDocument);
                response.Changes = await LinePositionSpanTextChange.Convert(originalDocument, changes);
            }

            return response;
        }

        private async Task<CodeRefactoringContext?> GetContext(CodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document != null)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var location = new TextSpan(position, 1);
                return new CodeRefactoringContext(document, location, (a) => actionsDestination.Add(a), CancellationToken.None);
            }

            //todo, handle context creation issues
            return null;
        }

        private async Task GetContextualCodeActions(CodeRefactoringContext? context)
        {
            if (!context.HasValue)
            {
                return;
            }

            if (_codeActionProviders != null)
            {
                foreach (var provider in _codeActionProviders)
                {
                    var providers = provider.Refactorings;

                    foreach (var codeActionProvider in providers)
                    {
                        await codeActionProvider.ComputeRefactoringsAsync(context.Value);
                    }
                }
            }
        }

    }
}
