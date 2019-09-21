using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{

    public class DocumentWithFixProvidersAndMatchingDiagnostics
    {
        private readonly DocumentDiagnostics _documentDiagnostics;

        // There are few usability optimizations in built in analyzers that requieres custom filtering in actions.
        // RemoveUnnecessaryImportsFixable is duplicate with CS8019 and both can be executed, selected CS8019 from these two.
        private static readonly ImmutableHashSet<string> _blacklistedDiagnostics = ImmutableHashSet.Create("RemoveUnnecessaryImportsFixable");

        private DocumentWithFixProvidersAndMatchingDiagnostics(CodeFixProvider provider, DocumentDiagnostics documentDiagnostics)
        {
            CodeFixProvider = provider;
            _documentDiagnostics = documentDiagnostics;
            FixAllProvider = provider.GetFixAllProvider();
        }

        public CodeFixProvider CodeFixProvider { get; }
        public FixAllProvider FixAllProvider { get; }
        public DocumentId DocumentId => _documentDiagnostics.DocumentId;
        public ProjectId ProjectId => _documentDiagnostics.ProjectId;
        public string DocumentPath => _documentDiagnostics.DocumentPath;
        public IEnumerable<Diagnostic> Diagnostics => _documentDiagnostics.Diagnostics.Where(x => HasFix(CodeFixProvider, x.Id));

        private static bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return codeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId)
                && !_blacklistedDiagnostics.Contains(diagnosticId);
        }

        public static ImmutableArray<DocumentWithFixProvidersAndMatchingDiagnostics> CreateWithMatchingProviders(ImmutableArray<CodeFixProvider> providers, DocumentDiagnostics documentDiagnostics)
        {
            return
                providers
                    .Select(provider => new DocumentWithFixProvidersAndMatchingDiagnostics(provider, documentDiagnostics))
                    .Where(x => x.Diagnostics.Any())
                    .Where(x => x.FixAllProvider != null)
                    .ToImmutableArray();
        }

        public async Task<CodeAction> RegisterCodeFixesAndGetCorrespondingAction(Document document)
        {
            CodeAction action = null;

            foreach (var diagnostic in Diagnostics)
            {
                var context = new CodeFixContext(
                document,
                diagnostic,
                (a, _) =>
                {
                    if (action == null)
                    {
                        action = a;
                    }
                },
                CancellationToken.None);

                await CodeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
            }

            return action;
        }
    }
}
