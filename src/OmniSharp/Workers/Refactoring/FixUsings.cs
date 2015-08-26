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
        public static async Task<FixUsingsResponse> FixUsings(OmnisharpWorkspace workspace, string fileName, Document document, SemanticModel semanticModel, bool wantsTextChanges)
        {
            var codeActionProvider = new RoslynCodeActionProvider();
            var ambiguous = await AddMissingUsings(fileName, workspace, document, semanticModel, codeActionProvider);
            await OrganizeUsings(fileName, workspace, document, semanticModel, codeActionProvider);
            document = workspace.GetDocument(fileName);
            var compilationUnitSyntax = (await AddLinqQuerySyntax(fileName, workspace, document, semanticModel));
            document = document.WithSyntaxRoot(compilationUnitSyntax);

            var response = new FixUsingsResponse();
            response.AmbiguousResults = ambiguous;
            if (!wantsTextChanges)
            {
                // return the new document
                var docText = await document.GetTextAsync();
                response.Buffer = docText.ToString();
            }
            else
            {
                // return the text changes
                var changes = await workspace.CurrentSolution.GetDocument(document.Id).GetTextChangesAsync(document);
                response.Changes = await LinePositionSpanTextChange.Convert(document, changes);
            }

            return response;
        }

        private static async Task<List<QuickFix>> AddMissingUsings(string fileName, OmnisharpWorkspace workspace, Document document, SemanticModel semanticModel, ICodeActionProvider codeActionProvider)
        {
            bool processMore = true;
            var ambiguousNodes = new List<SimpleNameSyntax>();
            var ambiguous = new List<QuickFix>();

            while (processMore)
            {
                bool updated = false;

                var syntaxNode = (await document.GetSyntaxTreeAsync()).GetRoot();
                var nodes = syntaxNode.DescendantNodes()
                    .OfType<SimpleNameSyntax>()
                    .Where(x => semanticModel.GetSymbolInfo(x).Symbol == null && !ambiguousNodes.Contains(x)).ToList();

                foreach (var node in nodes)
                {
                    document = workspace.GetDocument(fileName);
                    semanticModel = await document.GetSemanticModelAsync();
                    var sourceText = (await document.GetTextAsync());
                    var diagnostics = semanticModel.GetDiagnostics();
                    var location = node.Identifier.Span;
                    var usingOperations = new Dictionary<string, CodeActionOperation>();
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
                        var usingActions = GetUsingActions(document, codeActionProvider, pointDiagnostics, "using");
                        foreach (var action in usingActions)
                        {
                            var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
                            if (operations != null)
                            {
                                foreach (var codeOperation in operations)
                                {
                                    usingOperations.Add(action.Title, codeOperation);
                                }
                            }
                        }

                        if (usingOperations.Count() == 1)
                        {
                            //Only one operation - apply it
                            usingOperations.Single().Value.Apply(workspace, CancellationToken.None);
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
                                FileName = fileName,
                                Text = "`" + unresolvedText + "`" + " is ambiguous"
                            });
                        }
                    }
                }

                processMore = updated;
            }

            return ambiguous;
        }

        private static async Task OrganizeUsings(string fileName, OmnisharpWorkspace workspace, Document document, SemanticModel semanticModel, ICodeActionProvider codeActionProvider)
        {
            //Remove unneccessary usings
            var syntaxNode = (await document.GetSyntaxTreeAsync()).GetRoot();
            var nodes = syntaxNode.DescendantNodes().Where(x => x is UsingDirectiveSyntax);

            foreach (var node in nodes)
            {
                document = workspace.GetDocument(fileName);
                semanticModel = await document.GetSemanticModelAsync();
                var sourceText = (await document.GetTextAsync());
                var diagnostics = semanticModel.GetDiagnostics();
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
                    var usingActions = GetUsingActions(document, codeActionProvider, pointDiagnostics, "Remove Unnecessary Usings");
                    foreach (var action in usingActions)
                    {
                        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
                        if (operations != null)
                        {
                            foreach (var codeOperation in operations)
                            {
                                if (codeOperation != null)
                                {
                                    codeOperation.Apply(workspace, CancellationToken.None);
                                }
                            }
                        }
                    }
                }
            }

            //Sort usings
            var nRefactoryProvider = new NRefactoryCodeActionProvider();
            var sortActions = new List<CodeAction>();
            var refactoringContext = await GetRefactoringContext(document, sortActions);
            if (refactoringContext != null)
            {
                foreach (var refactoring in nRefactoryProvider.Refactorings)
                {
                    if (refactoring.ToString() != "ICSharpCode.NRefactory6.CSharp.Refactoring.SortUsingsAction")
                    {
                        continue;
                    }
                    await refactoring.ComputeRefactoringsAsync(refactoringContext.Value);
                }

                foreach (var action in sortActions)
                {
                    var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
                    if (operations != null)
                    {
                        foreach (var codeOperation in operations)
                        {
                            if (codeOperation != null)
                            {
                                codeOperation.Apply(workspace, CancellationToken.None);
                            }
                        }
                    }
                }
            }

            return;
        }

        private static async Task<CodeRefactoringContext?> GetRefactoringContext(Document document, List<CodeAction> actionsDestination)
        {
            var firstUsing = (await document.GetSyntaxTreeAsync()).GetRoot().DescendantNodes().Where(n => n is UsingDirectiveSyntax);
            if (firstUsing.Count() == 0)
            {
                return null;
            }
            var location = firstUsing.First().GetLocation().SourceSpan;

            return new CodeRefactoringContext(document, location, (a) => actionsDestination.Add(a), CancellationToken.None);
        }

        private static List<CodeAction> GetUsingActions(Document document, ICodeActionProvider codeActionProvider,
                ImmutableArray<Diagnostic> pointDiagnostics, string actionPrefix)
        {

            var actions = new List<CodeAction>();
            var context = new CodeFixContext(document, pointDiagnostics.First().Location.SourceSpan, pointDiagnostics, (a, d) => actions.Add(a), CancellationToken.None);
            var providers = codeActionProvider.CodeFixes;

            //Disable await warning since we dont need the result of the call. Else we need to use a throwaway variable.
#pragma warning disable 4014
            foreach (var provider in providers)
            {
                provider.RegisterCodeFixesAsync(context);
            }
#pragma warning restore 4014

            return actions.Where(a => a.Title.StartsWith(actionPrefix)).ToList();
        }

        private static async Task<CompilationUnitSyntax> AddLinqQuerySyntax(string fileName, OmnisharpWorkspace workspace, Document document, SemanticModel semanticModel)
        {
            var syntaxNode = (await document.GetSyntaxTreeAsync()).GetRoot();
            var compilationUnitSyntax = (CompilationUnitSyntax)syntaxNode;
            var usings = GetUsings(syntaxNode);
            if (HasLinqQuerySyntax(semanticModel, syntaxNode) && !usings.Contains("using System.Linq;"))
            {
                var linqName = SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"),
                        SyntaxFactory.IdentifierName("Linq"));
                var linq = SyntaxFactory.UsingDirective(linqName).NormalizeWhitespace()
                            .WithTrailingTrivia(SyntaxFactory.Whitespace(Environment.NewLine));
                compilationUnitSyntax = compilationUnitSyntax.AddUsings(linq);
            }

            return compilationUnitSyntax;
        }

        private static HashSet<string> GetUsings(SyntaxNode root)
        {
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().Select(u => u.ToString().Trim());
            return new HashSet<string>(usings);
        }

        private static bool HasLinqQuerySyntax(SemanticModel semanticModel, SyntaxNode syntaxNode)
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
#endif
