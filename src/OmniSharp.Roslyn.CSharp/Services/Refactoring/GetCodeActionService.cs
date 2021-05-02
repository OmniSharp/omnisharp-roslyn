using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.CodeAction;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.GetCodeAction, LanguageNames.CSharp)]
    public class GetCodeActionsService : IRequestHandler<GetCodeActionRequest, GetCodeActionsResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _codeActionProviders;

        [ImportingConstructor]
        public GetCodeActionsService(OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers)
        {
            _workspace = workspace;
            _codeActionProviders = providers;
        }

        public async Task<GetCodeActionsResponse> Handle(GetCodeActionRequest request)
        {
            var actions = new List<CodeAction>();
            var context = await GetContext(request, actions);
            await GetContextualCodeActions(context);
            return new GetCodeActionsResponse() { CodeActions = actions.Select(a => a.Title) };
        }

        private async Task<CodeRefactoringContext?> GetContext(CodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document != null)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.GetTextPosition(request);
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
