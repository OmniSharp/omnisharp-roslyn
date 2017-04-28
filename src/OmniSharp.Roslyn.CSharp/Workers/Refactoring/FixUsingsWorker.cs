using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Services;

namespace OmniSharp
{
    public class FixUsingsWorkerResponse
    {
        public IEnumerable<QuickFix> AmbiguousResults { get; set; }
        public Document Document { get; set; }
    }

    public class FixUsingsWorker
    {
        private readonly IEnumerable<ICodeActionProvider> _providers;
        private readonly CodeFixProvider _addImportProvider;
        private readonly CodeFixProvider _removeUnnecessaryUsingsProvider;

        public FixUsingsWorker(IEnumerable<ICodeActionProvider> providers)
        {
            _providers = providers;

            var codeFixProviders = providers.SelectMany(p => p.CodeFixes);

            _addImportProvider = FindCodeFixProviderByTypeFullName(codeFixProviders, CodeActionHelper.AddImportProviderName);
            _removeUnnecessaryUsingsProvider = FindCodeFixProviderByTypeFullName(codeFixProviders, CodeActionHelper.RemoveUnnecessaryUsingsProviderName);
        }

        private static CodeFixProvider FindCodeFixProviderByTypeFullName(IEnumerable<CodeFixProvider> providers, string fullName)
        {
            var provider = providers.FirstOrDefault(p => p.GetType().FullName == fullName);
            if (provider == null)
            {
                throw new InvalidOperationException($"Could not locate {fullName}");
            }

            return provider;
        }

        public async Task<FixUsingsWorkerResponse> FixUsingsAsync(Document document)
        {
            document = await AddMissingUsingsAsync(document);
            document = await RemoveUnnecessaryUsingsAsync(document);
            document = await TryAddLinqQuerySyntaxAsync(document);

            var ambiguous = await GetAmbiguousUsingsAsync(document);

            var response = new FixUsingsWorkerResponse()
            {
                AmbiguousResults = ambiguous,
                Document = document
            };

            response.AmbiguousResults = ambiguous;
            response.Document = document;

            return response;
        }

        private async Task<List<QuickFix>> GetAmbiguousUsingsAsync(Document document)
        {
            var ambiguousNodes = new List<SimpleNameSyntax>();
            var results = new List<QuickFix>();

            var semanticModel = await document.GetSemanticModelAsync();
            var root = await semanticModel.SyntaxTree.GetRootAsync();

            var simpleNames = root
                .DescendantNodes()
                .OfType<SimpleNameSyntax>()
                .Where(x => semanticModel.GetSymbolInfo(x).Symbol == null)
                .ToArray();

            foreach (var name in simpleNames)
            {
                if (ambiguousNodes.Contains(name))
                {
                    continue;
                }

                var diagnostics = await GetDiagnosticsAtSpanAsync(document, name.Identifier.Span, "CS0246", "CS1061", "CS0103");
                if (diagnostics.Any())
                {
                    var span = diagnostics.First().Location.SourceSpan;
                    if (diagnostics.Any(d => d.Location.SourceSpan != span))
                    {
                        continue;
                    }

                    var operations = await GetCodeFixOperationsAsync(_addImportProvider, document, span, diagnostics);

                    if (operations.Length > 1)
                    {
                        // More than one operation - ambiguous
                        ambiguousNodes.Add(name);
                        var unresolvedText = name.Identifier.ValueText;
                        var unresolvedLocation = name.GetLocation().GetLineSpan().StartLinePosition;

                        results.Add(
                            new QuickFix
                            {
                                Line = unresolvedLocation.Line,
                                Column = unresolvedLocation.Character,
                                FileName = document.FilePath,
                                Text = "`" + unresolvedText + "`" + " is ambiguous"
                            });
                    }
                }
            }

            return results;
        }

        private async Task<Document> AddMissingUsingsAsync(Document document)
        {
            while (true)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var root = await semanticModel.SyntaxTree.GetRootAsync();

                var unboundNames = root
                    .DescendantNodes()
                    .OfType<SimpleNameSyntax>()
                    .Where(name => semanticModel.GetSymbolInfo(name).Symbol == null)
                    .ToArray();

                var done = false;

                foreach (var name in unboundNames)
                {
                    var diagnostics = await GetDiagnosticsAtSpanAsync(document, name.Identifier.Span, "CS0246", "CS1061", "CS0103");
                    if (diagnostics.Any())
                    {
                        // Ensure that we only process diagnostics where each diagnostic has the same span.
                        var span = diagnostics.First().Location.SourceSpan;
                        if (diagnostics.Any(d => d.Location.SourceSpan != span))
                        {
                            continue;
                        }

                        var operations = await GetCodeFixOperationsAsync(_addImportProvider, document, span, diagnostics);

                        if (operations.Length == 1 && operations[0] is ApplyChangesOperation)
                        {
                            // Only one operation - apply it and loop back around
                            var newSolution = ((ApplyChangesOperation)operations[0]).ChangedSolution;
                            if (document.Project.Solution != newSolution)
                            {
                                document = newSolution.GetDocument(document.Id);
                                done = true;
                                break;
                            }
                        }
                    }
                }

                if (!done)
                {
                    break;
                }
            }

            return document;
        }

        private async Task<Document> RemoveUnnecessaryUsingsAsync(Document document)
        {
            // Remove unneccessary usings
            var root = await document.GetSyntaxRootAsync();
            var usingDirectives = root.DescendantNodes().OfType<UsingDirectiveSyntax>();

            var done = false;

            foreach (var usingDirective in usingDirectives)
            {
                var diagnostics = await GetDiagnosticsAtSpanAsync(document, usingDirective.Span, "CS0105", "CS8019");
                if (diagnostics.Any())
                {
                    // Ensure that we only process diagnostics where each diagnostic has the same span.
                    var span = diagnostics.First().Location.SourceSpan;
                    if (diagnostics.Any(d => d.Location.SourceSpan != span))
                    {
                        continue;
                    }

                    var operations = await GetCodeFixOperationsAsync(_removeUnnecessaryUsingsProvider, document, span, diagnostics);

                    foreach (var operation in operations.OfType<ApplyChangesOperation>())
                    {
                        if (operation != null)
                        {
                            if (document.Project.Solution != operation.ChangedSolution)
                            {
                                // Did one of the operations change the solution? If so, we're done.
                                document = operation.ChangedSolution.GetDocument(document.Id);
                                done = true;
                                break;
                            }
                        }
                    }
                }

                if (done)
                {
                    break;
                }
            }


            return document;
        }

        private async Task<Document> TryAddLinqQuerySyntaxAsync(Document document)
        {
            var root = await document.GetSyntaxRootAsync();
            var usings = GetAllUsings(root);

            if (HasLinqQuerySyntax(root) && !usings.Contains("using System.Linq;"))
            {
                var linqName = SyntaxFactory.QualifiedName(
                    SyntaxFactory.IdentifierName("System"),
                    SyntaxFactory.IdentifierName("Linq"));

                var linq = SyntaxFactory.UsingDirective(linqName)
                    .NormalizeWhitespace()
                    .WithTrailingTrivia(SyntaxFactory.Whitespace(Environment.NewLine));

                var newRoot = ((CompilationUnitSyntax)root).AddUsings(linq);
                document = document.WithSyntaxRoot(newRoot);
            }

            return document;
        }

        private async Task<ImmutableArray<CodeActionOperation>> GetCodeFixOperationsAsync(
            CodeFixProvider provider,
            Document document,
            TextSpan span,
            ImmutableArray<Diagnostic> diagnostics)
        {
            var codeFixes = new List<CodeAction>();
            var context = new CodeFixContext(
                document, span, diagnostics,
                registerCodeFix: (a, d) => codeFixes.Add(a),
                cancellationToken: CancellationToken.None);

            // Note: We're intentionally not checking CodeFixProvider.FixableDiagnosticIds here.
            // The problem is that some providers (like Remove Unnecessary Usings) only listen
            // for custom IDs produced by their associated diagnostic analyzer. Once we have a
            // proper diagnostic engine in OmniSharp, we may be able remove this.

            await provider.RegisterCodeFixesAsync(context);

            var getOperationsTasks = codeFixes
                .Select(a => a.GetOperationsAsync(CancellationToken.None));

            // Wait until all tasks to produce CodeActionOperations finish running in parallel.
            await Task.WhenAll(getOperationsTasks);

            return getOperationsTasks
                .Select(t => t.Result)
                .SelectMany(ops => ops)
                .ToImmutableArray();
        }

        private static HashSet<string> GetAllUsings(SyntaxNode root)
        {
            var usings = root
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(u => u.ToString().Trim());

            return new HashSet<string>(usings);
        }

        private async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAtSpanAsync(Document document, TextSpan span, params string[] diagnosticIds)
        {
            var semanticModel = await document.GetSemanticModelAsync();
            var diagnostics = semanticModel.GetDiagnostics();

            //Restrict diagnostics only to missing usings
            return diagnostics
                .Where(d => d.Location.SourceSpan.Contains(span) && diagnosticIds.Contains(d.Id))
                .ToImmutableArray();
        }

        private static bool HasLinqQuerySyntax(SyntaxNode root)
        {
            return root
                .DescendantNodes()
                .Any(IsLinqQuerySyntax);
        }

        private static bool IsLinqQuerySyntax(SyntaxNode node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.QueryExpression:
                case SyntaxKind.QueryBody:
                case SyntaxKind.FromClause:
                case SyntaxKind.LetClause:
                case SyntaxKind.JoinClause:
                case SyntaxKind.JoinIntoClause:
                case SyntaxKind.WhereClause:
                case SyntaxKind.OrderByClause:
                case SyntaxKind.AscendingOrdering:
                case SyntaxKind.DescendingOrdering:
                case SyntaxKind.SelectClause:
                case SyntaxKind.GroupClause:
                case SyntaxKind.QueryContinuation:
                    return true;

                default:
                    return false;
            }
        }
    }
}
