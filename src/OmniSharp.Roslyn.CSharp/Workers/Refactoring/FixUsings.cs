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
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Services;

namespace OmniSharp
{
    public class FixUsingsWorkerResponse
    {
        public IEnumerable<QuickFix> AmbiguousResults { get; set; }
        public Solution Solution { get; set; }
    }

    public class FixUsingsWorker
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOmnisharpAssemblyLoader _loader;
        private string _path;

        public FixUsingsWorker(ILoggerFactory loggerFactory, IOmnisharpAssemblyLoader loader)
        {
            _loggerFactory = loggerFactory;
            _loader = loader;
        }

        public async Task<FixUsingsWorkerResponse> FixUsings(Solution solution, IEnumerable<ICodeActionProvider> codeActionProviders, string path)
        {
            _path = path;
            solution = await AddMissingUsings(solution, codeActionProviders);
            solution = await RemoveUsings(solution, codeActionProviders);
            solution = await TryAddLinqQuerySyntax(solution);
            var ambiguous = await GetAmbiguousUsings(solution, codeActionProviders);
            var response = new FixUsingsWorkerResponse();
            response.AmbiguousResults = ambiguous;
            response.Solution = solution;

            return response;
        }

        private async Task<List<QuickFix>> GetAmbiguousUsings(Solution solution, IEnumerable<ICodeActionProvider> codeActionProviders)
        {
            var ambiguousNodes = new List<SimpleNameSyntax>();
            var ambiguous = new List<QuickFix>();

            var id = solution.GetDocumentIdsWithFilePath(_path).FirstOrDefault();
            var document = solution.GetDocument(id);
            var semanticModel = await document.GetSemanticModelAsync();

            var syntaxNode = (await document.GetSyntaxTreeAsync()).GetRoot();
            var nodes = syntaxNode.DescendantNodes()
                .OfType<SimpleNameSyntax>()
                .Where(x => semanticModel.GetSymbolInfo(x).Symbol == null && !ambiguousNodes.Contains(x)).ToList();

            foreach (var node in nodes)
            {
                var pointDiagnostics = await GetPointDiagnostics(document, node.Identifier.Span, new List<string>() { "CS0246", "CS1061", "CS0103" });
                if (pointDiagnostics.Any())
                {
                    var pointdiagfirst = pointDiagnostics.First().Location.SourceSpan;

                    if (pointDiagnostics.Any(d => d.Location.SourceSpan != pointdiagfirst))
                    {
                        continue;
                    }
                    var usingOperations = await GetUsingActions(document, codeActionProviders, pointDiagnostics, "using");

                    if (usingOperations.Count() > 1)
                    {
                        //More than one operation - ambiguous
                        ambiguousNodes.Add(node);
                        var unresolvedText = node.Identifier.ValueText;
                        var unresolvedLocation = node.GetLocation().GetLineSpan().StartLinePosition;
                        ambiguous.Add(new QuickFix
                        {
                            Line = unresolvedLocation.Line,
                            Column = unresolvedLocation.Character,
                            FileName = document.FilePath,
                            Text = "`" + unresolvedText + "`" + " is ambiguous"
                        });
                    }
                }
            }

            return ambiguous;
        }

        private async Task<Solution> AddMissingUsings(Solution solution, IEnumerable<ICodeActionProvider> codeActionProviders)
        {
            var id = solution.GetDocumentIdsWithFilePath(_path).FirstOrDefault();
            bool processMore = true;

            while (processMore)
            {
                var document = solution.GetDocument(id);
                var semanticModel = await document.GetSemanticModelAsync();

                bool updated = false;
                var syntaxNode = (await document.GetSyntaxTreeAsync()).GetRoot();
                var nodes = syntaxNode.DescendantNodes()
                    .OfType<SimpleNameSyntax>()
                    .Where(x => semanticModel.GetSymbolInfo(x).Symbol == null).ToList();

                foreach (var node in nodes)
                {
                    var pointDiagnostics = await GetPointDiagnostics(document, node.Identifier.Span, new List<string>() { "CS0246", "CS1061", "CS0103" });

                    if (pointDiagnostics.Any())
                    {
                        var pointdiagfirst = pointDiagnostics.First().Location.SourceSpan;
                        if (pointDiagnostics.Any(d => d.Location.SourceSpan != pointdiagfirst))
                        {
                            continue;
                        }

                        var usingOperations = (await GetUsingActions(document, codeActionProviders, pointDiagnostics, "using")).OfType<ApplyChangesOperation>();
                        if (usingOperations.Count() == 1)
                        {
                            //Only one operation - apply it
                            solution = usingOperations.First().ChangedSolution;
                            updated = true;
                            document = solution.GetDocument(id);
                            semanticModel = await document.GetSemanticModelAsync();
                        }
                    }
                }

                processMore = updated;
            }

            return solution;
        }

        private async Task<Solution> RemoveUsings(Solution solution, IEnumerable<ICodeActionProvider> codeActionProviders)
        {
            var id = solution.GetDocumentIdsWithFilePath(_path).FirstOrDefault();
            var document = solution.GetDocument(id);
            //Remove unneccessary usings
            var syntaxNode = (await document.GetSyntaxTreeAsync()).GetRoot();
            var nodes = syntaxNode.DescendantNodes().Where(x => x is UsingDirectiveSyntax);

            foreach (var node in nodes)
            {
                var sourceText = (await document.GetTextAsync());
                var actions = new List<CodeAction>();
                var pointDiagnostics = await GetPointDiagnostics(document, node.Span, new List<string>() { "CS0105", "CS8019" });

                if (pointDiagnostics.Any())
                {
                    var pointdiagfirst = pointDiagnostics.First().Location.SourceSpan;
                    if (pointDiagnostics.Any(d => d.Location.SourceSpan != pointdiagfirst))
                    {
                        continue;
                    }

                    var usingActions = await GetUsingActions(document, codeActionProviders, pointDiagnostics, "Remove Unnecessary Usings");

                    foreach (var codeOperation in usingActions.OfType<ApplyChangesOperation>())
                    {
                        if (codeOperation != null)
                        {
                            solution = codeOperation.ChangedSolution;
                            document = solution.GetDocument(id);
                        }
                    }
                }
            }


            return solution;
        }

        private async Task<Solution> TryAddLinqQuerySyntax(Solution solution)
        {
            var id = solution.GetDocumentIdsWithFilePath(_path).FirstOrDefault();
            var document = solution.GetDocument(id);
            var semanticModel = await document.GetSemanticModelAsync();
            var fileName = document.FilePath;
            var syntaxNode = (await document.GetSyntaxTreeAsync()).GetRoot();
            var compilationUnitSyntax = (CompilationUnitSyntax)syntaxNode;
            var usings = GetUsings(syntaxNode);
            if (HasLinqQuerySyntax(semanticModel, syntaxNode) && !usings.Contains("using System.Linq;"))
            {
                var linqName = SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"),
                        SyntaxFactory.IdentifierName("Linq"));
                var linq = SyntaxFactory.UsingDirective(linqName).NormalizeWhitespace()
                            .WithTrailingTrivia(SyntaxFactory.Whitespace(Environment.NewLine));
                var oldSolution = solution;
                document = document.WithSyntaxRoot(compilationUnitSyntax.AddUsings(linq));
                var newDocText = await document.GetTextAsync();
                var newSolution = oldSolution.WithDocumentText(document.Id, newDocText);
                solution = newSolution;
                semanticModel = await document.GetSemanticModelAsync();
                document = solution.GetDocument(id);
            }

            return solution;
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

        private async Task<IEnumerable<CodeActionOperation>> GetUsingActions(Document document, IEnumerable<ICodeActionProvider> codeActionProviders,
                ImmutableArray<Diagnostic> pointDiagnostics, string actionPrefix)
        {
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(document, pointDiagnostics.First().Location.SourceSpan, pointDiagnostics, (a, d) => actions.Add(a), CancellationToken.None);
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

        private async Task<ImmutableArray<Diagnostic>> GetPointDiagnostics(Document document, TextSpan location, List<string> diagnosticIds)
        {
            var semanticModel = await document.GetSemanticModelAsync();
            var diagnostics = semanticModel.GetDiagnostics();

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
