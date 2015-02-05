#if ASPNET50
using ICSharpCode.NRefactory6.CSharp.Refactoring;
#endif
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            await GetContextualCodeActions(request, context);
            return new GetCodeActionsResponse() { CodeActions = actions.Select(a => a.Title) };
        }

        [HttpPost("runcodeaction")]
        public async Task<RunCodeActionsResponse> RunCodeAction([FromBody]CodeActionRequest request)
        {
            var actions = new List<CodeAction>();
            var context = await GetContext(request, actions);
            await GetContextualCodeActions(request,context);
            if (request.CodeAction > actions.Count())
                return new RunCodeActionsResponse();

            //run the code action


            //return the new document
            var sourceText = await context.Value.Document.GetTextAsync();
            return new RunCodeActionsResponse { Text = sourceText.ToString() };
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

        private async Task<bool> GetContextualCodeActions(CodeActionRequest request, CodeRefactoringContext? context)
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
                return true;
            }
            return false;
        }

    }


    public class CodeActionRequest : Request
    {
        public int CodeAction { get; set; }
        public int? SelectionStartColumn { get; set; }
        public int? SelectionStartLine { get; set; }
        public int? SelectionEndColumn { get; set; }
        public int? SelectionEndLine { get; set; }
    }

    public class CodeActionProviders
    {
        public IEnumerable<CodeRefactoringProvider> GetProviders()
        {
            var types = Assembly.GetAssembly(typeof(UseVarKeywordAction))
                                .GetTypes()
                                .Where(t => typeof(CodeRefactoringProvider).IsAssignableFrom(t));

            IEnumerable<CodeRefactoringProvider> providers =
                types
                    .Where(type => !type.IsInterface
                            && !type.IsAbstract
                            && !type.ContainsGenericParameters) //TODO: handle providers with generic params 
                    .Select(type => (CodeRefactoringProvider)Activator.CreateInstance(type));

            return providers;
        }
    }

    public class GetCodeActionsResponse
    {
        public IEnumerable<string> CodeActions { get; set; }
    }
    public class RunCodeActionsResponse
    {
        public string Text { get; set; }
    }
#endif
}