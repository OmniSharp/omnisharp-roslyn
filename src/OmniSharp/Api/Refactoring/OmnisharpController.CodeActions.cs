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
#if ASPNET50
    public partial class OmnisharpController
    {
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
            var providers = new CodeActionProviders().GetProviders();
            if (context.HasValue)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        await provider.ComputeRefactoringsAsync(context.Value);
                    }
                    catch (Exception) { }
                }
            }
        }
    }
#endif
}