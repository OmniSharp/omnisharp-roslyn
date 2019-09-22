using System.Collections;
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

        // http://source.roslyn.io/#Microsoft.VisualStudio.LanguageServices.CSharp/LanguageService/CSharpCodeCleanupFixer.cs,d9a375db0f1e430e,references
        // CS8019 isn't directly used (via roslyn) but has an analyzer that report different diagnostic based on CS8019 to improve user experience.
        private static readonly Dictionary<string, string> _customDiagVsFixMap = new Dictionary<string, string>
        {
            { "CS8019", "RemoveUnnecessaryImportsFixable" }
        };

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

        public IEnumerable<(string id, string messsage)> FixableDiagnostics => _documentDiagnostics.Diagnostics
            .Where(x => HasFixToShow(x.Id))
            .Select(x => (x.Id, x.GetMessage()));

        private bool HasFixToShow(string diagnosticId)
        {
            return CodeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId) && !_customDiagVsFixMap.ContainsValue(diagnosticId)
                || (_customDiagVsFixMap.ContainsKey(diagnosticId) && CodeFixProvider.FixableDiagnosticIds.Any(id => id == _customDiagVsFixMap[diagnosticId]));
        }

        public bool HasFixForId(string diagnosticId)
        {
            return CodeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId && !_customDiagVsFixMap.ContainsKey(diagnosticId))
                || (_customDiagVsFixMap.ContainsKey(diagnosticId) && CodeFixProvider.FixableDiagnosticIds.Any(id => id == _customDiagVsFixMap[diagnosticId]));
        }

        public static ImmutableArray<DocumentWithFixProvidersAndMatchingDiagnostics> CreateWithMatchingProviders(ImmutableArray<CodeFixProvider> providers, DocumentDiagnostics documentDiagnostics)
        {
            return
                providers
                    .Select(provider => new DocumentWithFixProvidersAndMatchingDiagnostics(provider, documentDiagnostics))
                    .Where(x => x._documentDiagnostics.Diagnostics.Any(d => x.HasFixToShow(d.Id)) || x._documentDiagnostics.Diagnostics.Any(d => x.HasFixForId(d.Id)))
                    .Where(x => x.FixAllProvider != null)
                    .ToImmutableArray();
        }

        public async Task<CodeAction> RegisterCodeFixesAndGetCorrespondingAction(Document document)
        {
            CodeAction action = null;

            foreach (var diagnostic in _documentDiagnostics.Diagnostics.Where(x => HasFixForId(x.Id)))
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
