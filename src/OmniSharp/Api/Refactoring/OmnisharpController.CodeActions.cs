using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp
{
    public class CodeActionController
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _codeActionProviders;
        private Document _originalDocument;

        public CodeActionController(OmnisharpWorkspace workspace, IEnumerable<ICodeActionProvider> providers)
        {
            _workspace = workspace;
            _codeActionProviders = providers;
        }

        [HttpPost("getcodeactions")]
        public async Task<GetCodeActionsResponse> GetCodeActions(CodeActionRequest request)
        {
            var actions = await GetActions(request);
            return new GetCodeActionsResponse() { CodeActions = actions.Select(a => a.Title) };
        }

        [HttpPost("runcodeaction")]
        public async Task<RunCodeActionResponse> RunCodeAction(CodeActionRequest request)
        {
            var actions = await GetActions(request);

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

            var response = new RunCodeActionResponse();
            if (!request.WantsTextChanges)
            {
                // return the new document
                var sourceText = await _workspace.CurrentSolution.GetDocument(_originalDocument.Id).GetTextAsync();
                response.Text = sourceText.ToString();
            }
            else
            {
                // return the text changes
                var changes = await _workspace.CurrentSolution.GetDocument(_originalDocument.Id).GetTextChangesAsync(_originalDocument);
                response.Changes = await LinePositionSpanTextChange.Convert(_originalDocument, changes);
            }

            return response;
        }

        private async Task<IEnumerable<CodeAction>> GetActions(CodeActionRequest request)
        {
            var actions = new List<CodeAction>();
            _originalDocument = _workspace.GetDocument(request.FileName);
            if (_originalDocument == null)
                return actions;

            var refactoringContext = await GetRefactoringContext(request, actions);
            var codeFixContext = await GetCodeFixContext(request, actions);
            await CollectRefactoringActions(refactoringContext);
            await CollectCodeFixActions(codeFixContext);
            actions.Reverse();
            return actions;
        }

        private async Task<CodeRefactoringContext?> GetRefactoringContext(CodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var sourceText = await _originalDocument.GetTextAsync();
            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
            var location = new TextSpan(position, 1);
            return new CodeRefactoringContext(_originalDocument, location, (a) => actionsDestination.Add(a), CancellationToken.None);
        }

        private async Task<CodeFixContext?> GetCodeFixContext(CodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var sourceText = await _originalDocument.GetTextAsync();
            var semanticModel = await _originalDocument.GetSemanticModelAsync();
            var diagnostics = semanticModel.GetDiagnostics();
            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));

            var pointDiagnostics = diagnostics.Where(d => d.Location.SourceSpan.Contains(position)).ToImmutableArray();

            if (pointDiagnostics.Any())
                return new CodeFixContext(_originalDocument, pointDiagnostics.First().Location.SourceSpan, pointDiagnostics, (a, d) => actionsDestination.Add(a), CancellationToken.None);
            return null;
        }

        private static readonly HashSet<string> _blacklist = new HashSet<string> {
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider",

            "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddMissingReference.AddMissingReferenceCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.Async.CSharpConvertToAsyncMethodCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpChangeToIEnumerableCodeFixProvider"
        };

        private async Task CollectCodeFixActions(CodeFixContext? fixContext)
        {
            foreach (var provider in _codeActionProviders)
            {
                if (fixContext.HasValue)
                {
                    foreach (var codeFix in provider.CodeFixes)
                    {
                        if (!_blacklist.Contains(codeFix.ToString()))
                        {
                            await codeFix.RegisterCodeFixesAsync(fixContext.Value);
                        }
                    }
                }
            }
        }

        private async Task CollectRefactoringActions(CodeRefactoringContext? refactoringContext)
        {
            foreach (var provider in _codeActionProviders)
            {
                if (refactoringContext.HasValue)
                {
                    foreach (var refactoring in provider.Refactorings)
                    {
                        // if (refactoring.ToString().EndsWith("InvertIfCodeRefactoringProvider"))
                        {
                            System.Console.WriteLine(refactoring);
                            // if (!_blacklist.Contains(refactoring.ToString()))
                            {
                                await refactoring.ComputeRefactoringsAsync(refactoringContext.Value);
                            }
                        }
                    }
                }
            }
        }
    }
}
