using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp
{
    public class CodeActionController
    {
        private OmnisharpWorkspace _workspace;
        private ICodeActionProvider _codeActionProvider;

        public CodeActionController(OmnisharpWorkspace workspace, ICodeActionProvider provider)
        {
            _workspace = workspace;
            _codeActionProvider = provider;
        }

        [HttpPost("getcodeactions")]
        public async Task<GetCodeActionsResponse> GetCodeActions([FromBody]CodeActionRequest request)
        {
            var actions = new List<CodeAction>();
            var context = await GetContext(request, actions);
            await GetContextualCodeActions(context);
            return new GetCodeActionsResponse() { CodeActions = actions.Select(a => a.Title) };
        }

        [HttpPost("runcodeaction")]
        public async Task<RunCodeActionResponse> RunCodeAction([FromBody]CodeActionRequest request)
        {
            var actions = new List<CodeAction>();
            var context = await GetContext(request, actions);
            await GetContextualCodeActions(context);
            if (request.CodeAction > actions.Count())
                return new RunCodeActionResponse();

            var action = actions.ElementAt(request.CodeAction);
            //this line fails \/ ;(
            var preview = await action.GetPreviewOperationsAsync(CancellationToken.None);

            //return the new document
            var sourceText = await context.Value.Document.GetTextAsync();
            return new RunCodeActionResponse { Text = sourceText.ToString() };
        }

        private async Task<CodeRefactoringContext?> GetContext(CodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document != null)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.SelectionStartLine.Value - 1, request.SelectionStartColumn.Value - 1));
                var location = new TextSpan(position, 1);
                return new CodeRefactoringContext(document, location, (a) => actionsDestination.Add(a), new System.Threading.CancellationToken());
            }
            //todo, handle context creation issues
            return null;
        }
        private async Task GetContextualCodeActions(CodeRefactoringContext? context)
        {
            if (_codeActionProvider != null)
            {
                var providers = _codeActionProvider.GetProviders();
                if (context.HasValue)
                {
                    foreach (var codeActionProvider in providers)
                    {
                        //remove this try catch once the Missing Method stuff subsides.
                        try
                        {
                            await codeActionProvider.ComputeRefactoringsAsync(context.Value);
                        }
                        catch (Exception) { }
                    }
                }
            }
        }

    }
}