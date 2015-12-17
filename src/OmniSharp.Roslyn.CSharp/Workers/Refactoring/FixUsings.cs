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
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Services;

namespace OmniSharp
{
    public class FixUsingsWorker
    {
        private OmnisharpWorkspace _workspace;
        private Document _document;
        private SemanticModel _semanticModel;

        public async Task<FixUsingsResponse> FixUsings(OmnisharpWorkspace workspace, IEnumerable<ICodeActionProvider> codeActionProviders, Document document)
        {
            _workspace = workspace;
            _document = document;
            _semanticModel = await document.GetSemanticModelAsync();
            await AddMissingUsings(codeActionProviders);
            await RemoveUsings(codeActionProviders);
#if DNX451
            await SortUsings();
#endif
            await TryAddLinqQuerySyntax();
            var ambiguous = await GetAmbiguousUsings(codeActionProviders);
            var response = new FixUsingsResponse();
            response.AmbiguousResults = ambiguous;

            return response;
        }

        private async Task<List<QuickFix>> GetAmbiguousUsings(IEnumerable<ICodeActionProvider> codeActionProviders)
        {
            var ambiguousNodes = new List<SimpleNameSyntax>();
            var ambiguous = new List<QuickFix>();

            var syntaxNode = (await _document.GetSyntaxTreeAsync()).GetRoot();
            var nodes = syntaxNode.DescendantNodes()
                .OfType<SimpleNameSyntax>()
                .Where(x => _semanticModel.GetSymbolInfo(x).Symbol == null && !ambiguousNodes.Contains(x)).ToList();

            foreach (var node in nodes)
            {
                var pointDiagnostics = await GetPointDiagnostics(node.Identifier.Span, new List<string>() { "CS0246", "CS1061", "CS0103" });
                if (pointDiagnostics.Any())
                {
                    var pointdiagfirst = pointDiagnostics.First().Location.SourceSpan;

                    if (pointDiagnostics.Any(d => d.Location.SourceSpan != pointdiagfirst))
                    {
                        continue;
                    }
                    var usingOperations = await GetUsingActions(codeActionProviders, pointDiagnostics, "using");

                    if (usingOperations.Count() > 1)
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

            return ambiguous;
        }

        private async Task AddMissingUsings(IEnumerable<ICodeActionProvider> codeActionProviders)
        {
            bool processMore = true;

            while (processMore)
            {
                bool updated = false;
                var syntaxNode = (await _document.GetSyntaxTreeAsync()).GetRoot();
                var nodes = syntaxNode.DescendantNodes()
                    .OfType<SimpleNameSyntax>()
                    .Where(x => _semanticModel.GetSymbolInfo(x).Symbol == null).ToList();

                foreach (var node in nodes)
                {
                    var pointDiagnostics = await GetPointDiagnostics(node.Identifier.Span, new List<string>() { "CS0246", "CS1061", "CS0103" });

                    if (pointDiagnostics.Any())
                    {
                        var pointdiagfirst = pointDiagnostics.First().Location.SourceSpan;
                        if (pointDiagnostics.Any(d => d.Location.SourceSpan != pointdiagfirst))
                        {
                            continue;
                        }
                        var usingOperations = await GetUsingActions(codeActionProviders, pointDiagnostics, "using");

                        if (usingOperations.Count() == 1)
                        {
                            //Only one operation - apply it
                            usingOperations.Single().Apply(_workspace, CancellationToken.None);
                            updated = true;
                        }
                    }
                }

                processMore = updated;
            }

            return;
        }

        private async Task RemoveUsings(IEnumerable<ICodeActionProvider> codeActionProviders)
        {
            //Remove unneccessary usings
            var syntaxNode = (await _document.GetSyntaxTreeAsync()).GetRoot();
            var nodes = syntaxNode.DescendantNodes().Where(x => x is UsingDirectiveSyntax);

            foreach (var node in nodes)
            {
                var sourceText = (await _document.GetTextAsync());
                var actions = new List<CodeAction>();
                var pointDiagnostics = await GetPointDiagnostics(node.Span, new List<string>() { "CS0105", "CS8019" });

                if (pointDiagnostics.Any())
                {
                    var pointdiagfirst = pointDiagnostics.First().Location.SourceSpan;
                    if (pointDiagnostics.Any(d => d.Location.SourceSpan != pointdiagfirst))
                    {
                        continue;
                    }
                    var usingActions = await GetUsingActions(codeActionProviders, pointDiagnostics, "Remove Unnecessary Usings");

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

#if DNX451
        private async Task SortUsings()
        {
            //Sort usings
            var nRefactoryProvider = new NRefactoryCodeActionProvider();
            var sortActions = new List<CodeAction>();
            var refactoringContext = await GetRefactoringContext(_document, sortActions);
            if (refactoringContext != null)
            {
                nRefactoryProvider.Refactorings
                                  .FirstOrDefault(r => r is ICSharpCode.NRefactory6.CSharp.Refactoring.SortUsingsAction)
                                 ?.ComputeRefactoringsAsync(refactoringContext.Value);

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
#endif

        private async Task TryAddLinqQuerySyntax()
        {
            var fileName = _document.FilePath;
            var syntaxNode = (await _document.GetSyntaxTreeAsync()).GetRoot();
            var compilationUnitSyntax = (CompilationUnitSyntax)syntaxNode;
            var usings = GetUsings(syntaxNode);
            if (HasLinqQuerySyntax(_semanticModel, syntaxNode) && !usings.Contains("using System.Linq;"))
            {
                var linqName = SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"),
                        SyntaxFactory.IdentifierName("Linq"));
                var linq = SyntaxFactory.UsingDirective(linqName).NormalizeWhitespace()
                            .WithTrailingTrivia(SyntaxFactory.Whitespace(Environment.NewLine));
                var oldSolution = _workspace.CurrentSolution;
                _document = _document.WithSyntaxRoot(compilationUnitSyntax.AddUsings(linq));
                var newDocText = await _document.GetTextAsync();
                var newSolution = oldSolution.WithDocumentText(_document.Id, newDocText);
                _workspace.TryApplyChanges(newSolution);
                _semanticModel = await _document.GetSemanticModelAsync();
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

        private async Task<IEnumerable<CodeActionOperation>> GetUsingActions(IEnumerable<ICodeActionProvider> codeActionProviders,
                ImmutableArray<Diagnostic> pointDiagnostics, string actionPrefix)
        {
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(_document, pointDiagnostics.First().Location.SourceSpan, pointDiagnostics, (a, d) => actions.Add(a), CancellationToken.None);
            var providers = codeActionProviders.SelectMany(x => x.CodeFixes);

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

        private async Task<ImmutableArray<Diagnostic>> GetPointDiagnostics(TextSpan location, List<string> diagnosticIds)
        {
            _document = _workspace.GetDocument(_document.FilePath);
            _semanticModel = await _document.GetSemanticModelAsync();
            var diagnostics = _semanticModel.GetDiagnostics();

            //Restrict diagnostics only to missing usings
            return diagnostics.Where(d => d.Location.SourceSpan.Contains(location) &&
                    diagnosticIds.Contains(d.Id)).ToImmutableArray();
        }

        private bool HasLinqQuerySyntax(SemanticModel semanticModel, SyntaxNode syntaxNode)
        {
            return syntaxNode.DescendantNodes()
                .Any(x => x.Kind() == SyntaxKind.QueryExpression
                        || x.Kind() == SyntaxKind.QueryBody
                        || x.Kind() == SyntaxKind.FromClause
                        || x.Kind() == SyntaxKind.LetClause
                        || x.Kind() == SyntaxKind.JoinClause
                        || x.Kind() == SyntaxKind.JoinClause
                        || x.Kind() == SyntaxKind.JoinIntoClause
                        || x.Kind() == SyntaxKind.WhereClause
                        || x.Kind() == SyntaxKind.OrderByClause
                        || x.Kind() == SyntaxKind.AscendingOrdering
                        || x.Kind() == SyntaxKind.DescendingOrdering
                        || x.Kind() == SyntaxKind.SelectClause
                        || x.Kind() == SyntaxKind.GroupClause
                        || x.Kind() == SyntaxKind.QueryContinuation
                    );
        }
    }
}
