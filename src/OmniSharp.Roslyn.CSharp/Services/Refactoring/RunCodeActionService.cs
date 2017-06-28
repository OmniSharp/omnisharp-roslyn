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
using OmniSharp.Models.CodeAction;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.RunCodeAction, LanguageNames.CSharp)]
    public class RunCodeActionsService : IRequestHandler<RunCodeActionRequest, RunCodeActionResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _codeActionProviders;

        [ImportingConstructor]
        public RunCodeActionsService(OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers)
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

            var oldDocument = context.Value.Document;
            var newDocument = _workspace.CurrentSolution.GetDocument(oldDocument.Id);

            var response = new RunCodeActionResponse();

            if (!request.WantsTextChanges)
            {
                // return the text of the new document
                var newText = await newDocument.GetTextAsync();
                response.Text = newText.ToString();
            }
            else
            {
                // return the text changes
                response.Changes = await TextChanges.GetAsync(newDocument, oldDocument);
            }

            return response;
        }

        private async Task<CodeRefactoringContext?> GetContext(CodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document != null)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
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
                    var providers = provider.CodeRefactoringProviders;

                    foreach (var codeActionProvider in providers)
                    {
                        await codeActionProvider.ComputeRefactoringsAsync(context.Value);
                    }
                }
            }
        }

    }
}
