using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{

    public class DocumentWithFixAll
    {
        private readonly DocumentDiagnostics _documentDiagnostics;

        private DocumentWithFixAll(CodeFixProvider provider, DocumentDiagnostics documentDiagnostics)
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

        public ImmutableArray<(string id, string message)> GetAvailableFixableDiagnostics() => _documentDiagnostics.Diagnostics.Where(x => HasFix(CodeFixProvider, x.Id)).Select(x => (x.Id, x.GetMessage())).ToImmutableArray();

        private static bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return codeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId);
        }

        public static ImmutableArray<DocumentWithFixAll> CreateWithMatchingProviders(ImmutableArray<CodeFixProvider> providers, DocumentDiagnostics documentDiagnostics)
        {
            return
                providers
                    .Where(provider => provider.GetFixAllProvider() != null)
                    .Select(provider => new DocumentWithFixAll(provider, documentDiagnostics))
                    .Where(x => x.GetAvailableFixableDiagnostics().Any())
                    .ToImmutableArray();
        }
    }
}