using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{

    public class AvailableFixAllDiagnosticProvider
    {
        private readonly DocumentDiagnostics _documentDiagnostics;

        private AvailableFixAllDiagnosticProvider(CodeFixProvider provider, DocumentDiagnostics documentDiagnostics)
        {
            CodeFixProvider = provider;
            _documentDiagnostics = documentDiagnostics;
            FixAllProvider = provider.GetFixAllProvider();
        }

        public CodeFixProvider CodeFixProvider { get; }
        public FixAllProvider FixAllProvider { get; }

        public ImmutableArray<(string id, string message)> GetAvailableFixableDiagnostics() => _documentDiagnostics.Diagnostics.Where(x => HasFix(CodeFixProvider, x.Id)).Select(x => (x.Id, x.GetMessage())).ToImmutableArray();

        private static bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return codeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId);
        }

        private static AvailableFixAllDiagnosticProvider CreateOrDefault(CodeFixProvider provider, DocumentDiagnostics diagnostics)
        {
            if(provider.GetFixAllProvider() == default)
                return null;

            var result = new AvailableFixAllDiagnosticProvider(provider, diagnostics);

            if(!result.GetAvailableFixableDiagnostics().Any())
                return null;

            return result;
        }

        public static ImmutableArray<AvailableFixAllDiagnosticProvider> Create(ImmutableArray<CodeFixProvider> providers, DocumentDiagnostics documentDiagnostics)
        {
            return
                providers
                    .Select(x => CreateOrDefault(x, documentDiagnostics))
                    .Where(x => x != default)
                    .ToImmutableArray();
        }
    }
}