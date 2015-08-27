#if DNX451
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp
{
    public class FixUsingsWorker
    {
        private OmnisharpWorkspace _workspace;
        private Document _document;
        private SemanticModel _semanticModel;

        public async Task<FixUsingsResponse> FixUsings(OmnisharpWorkspace workspace, Document document)
        {
            _workspace = workspace;
            _document = document;
            _semanticModel = await document.GetSemanticModelAsync();
            var ambiguous = await AddMissingUsings();
            await RemoveUsings();
            await SortUsings();

            var response = new FixUsingsResponse();
            response.AmbiguousResults = ambiguous;
            return response;
        }

        private async Task<List<QuickFix>> AddMissingUsings()
        {
            bool processMore = true;
            var ambiguousNodes = new List<SimpleNameSyntax>();
            var ambiguous = new List<QuickFix>();

            while (processMore)
            {
                bool updated = false;
                var syntaxNode = (await _document.GetSyntaxTreeAsync()).GetRoot();
                var nodes = syntaxNode.DescendantNodes()
                    .OfType<SimpleNameSyntax>()
                    .Where(x => _semanticModel.GetSymbolInfo(x).Symbol == null && !ambiguousNodes.Contains(x)).ToList();

                foreach (var node in nodes)
                {
                    _document = _workspace.GetDocument(_document.FilePath);
                    _semanticModel = await _document.GetSemanticModelAsync();
                    var diagnostics = _semanticModel.GetDiagnostics();
                    var location = node.Identifier.Span;

                    //Restrict diagnostics only to missing usings
                    var pointDiagnostics = diagnostics.Where(d => d.Location.SourceSpan.Contains(location) &&
                            (d.Id == "CS0246" || d.Id == "CS1061" || d.Id == "CS0103")).ToImmutableArray();

                    if (pointDiagnostics.Any())
                    {
                        var pointdiagfirst = pointDiagnostics.First().Location.SourceSpan;
                        if (pointDiagnostics.Any(d => d.Location.SourceSpan != pointdiagfirst))
                        {
                            continue;
                        }
                        var usingOperations = await GetUsingActions(new RoslynCodeActionProvider(), pointDiagnostics, "using");

                        if (usingOperations.Count() == 1)
                        {
                            //Only one operation - apply it
                            usingOperations.Single().Apply(_workspace, CancellationToken.None);
                            updated = true;
                        }
                        else if (usingOperations.Count() > 1)
                        {
                            //More than one operation - ambiguous
                            ambiguousNodes.Add(node);
                            var unresolvedText = node.Identifier.ValueText;
                            var unresolvedLocation = node.GetLocation().GetLineSpan().StartLinePosition;
                            ambiguous.Add(new QuickFix
                            {
                                Line = unresolvedLocation.Line + 1,
                                Column = unresolvedLocation.Character + 1,
                                FileName = _document.FilePath,
                                Text = "`" + unresolvedText + "`" + " is ambiguous"
                            });
                        }
                    }
                }

                processMore = updated;
            }

            return ambiguous;
        }

        private async Task RemoveUsings()
        {
            var codeActionProvider = new RoslynCodeActionProvider();
            //Remove unneccessary usings
            var syntaxNode = (await _document.GetSyntaxTreeAsync()).GetRoot();
            var nodes = syntaxNode.DescendantNodes().Where(x => x is UsingDirectiveSyntax);

            foreach (var node in nodes)
            {
                _document = _workspace.GetDocument(_document.FilePath);
                _semanticModel = await _document.GetSemanticModelAsync();
                var sourceText = (await _document.GetTextAsync());
                var diagnostics = _semanticModel.GetDiagnostics();
                var location = node.Span;
                var actions = new List<CodeAction>();
                //Restrict diagnostics only to unneccessary and duplicate usings
                var pointDiagnostics = diagnostics.Where(d => d.Location.SourceSpan.Contains(location) &&
                        (d.Id == "CS0105" || d.Id == "CS8019")).ToImmutableArray();

                if (pointDiagnostics.Any())
                {
                    var pointdiagfirst = pointDiagnostics.First().Location.SourceSpan;
                    if (pointDiagnostics.Any(d => d.Location.SourceSpan != pointdiagfirst))
                    {
                        continue;
                    }
                    var usingActions = await GetUsingActions(codeActionProvider, pointDiagnostics, "Remove Unnecessary Usings");

                    foreach (var codeOperation in usingActions)
                    {
                        if (codeOperation != null)
                        {
                            codeOperation.Apply(_workspace, CancellationToken.None);
                        }
                    }
                }
            }

            return;
        }

        private async Task SortUsings()
        {
            //Sort usings
            var nRefactoryProvider = new NRefactoryCodeActionProvider();
            var sortActions = new List<CodeAction>();
            var refactoringContext = await GetRefactoringContext(_document, sortActions);
            if (refactoringContext != null)
            {
                var sortUsingsAction = nRefactoryProvider.Refactorings
                    .First(r => r is ICSharpCode.NRefactory6.CSharp.Refactoring.SortUsingsAction);

                await sortUsingsAction.ComputeRefactoringsAsync(refactoringContext.Value);

                foreach (var action in sortActions)
                {
                    var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
                    if (operations != null)
                    {
                        foreach (var codeOperation in operations)
                        {
                            if (codeOperation != null)
                            {
                                codeOperation.Apply(_workspace, CancellationToken.None);
                            }
                        }
                    }
                }
            }
        }

        private static async Task<CodeRefactoringContext?> GetRefactoringContext(Document document, List<CodeAction> actionsDestination)
        {
            var firstUsing = (await document.GetSyntaxTreeAsync()).GetRoot().DescendantNodes().FirstOrDefault(n => n is UsingDirectiveSyntax);
            if (firstUsing == null)
            {
                return null;
            }
            var location = firstUsing.GetLocation().SourceSpan;

            return new CodeRefactoringContext(document, location, (a) => actionsDestination.Add(a), CancellationToken.None);
        }

        private async Task<IEnumerable<CodeActionOperation>> GetUsingActions(ICodeActionProvider codeActionProvider,
                ImmutableArray<Diagnostic> pointDiagnostics, string actionPrefix)
        {
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(_document, pointDiagnostics.First().Location.SourceSpan, pointDiagnostics, (a, d) => actions.Add(a), CancellationToken.None);
            var providers = codeActionProvider.CodeFixes;

            //Disable await warning since we dont need the result of the call. Else we need to use a throwaway variable.
#pragma warning disable 4014
            foreach (var provider in providers)
            {
                provider.RegisterCodeFixesAsync(context);
            }
#pragma warning restore 4014

            var tasks = actions.Where(a => a.Title.StartsWith(actionPrefix))
                    .Select(async a => await a.GetOperationsAsync(CancellationToken.None)).ToList();
            return (await Task.WhenAll(tasks)).SelectMany(x => x);
        }

        private static HashSet<string> GetUsings(SyntaxNode root)
        {
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().Select(u => u.ToString().Trim());
            return new HashSet<string>(usings);
        }
    }
}
#endif
