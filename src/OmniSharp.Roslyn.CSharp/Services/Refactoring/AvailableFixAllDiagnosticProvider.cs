using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{

    public class AvailableFixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        protected AvailableFixAllDiagnosticProvider(CodeFixProvider provider, IEnumerable<Diagnostic> diagnostics)
        {
            CodeFixProvider = provider;
            FixAllProvider = provider.GetFixAllProvider();
            MatchingDiagnostics = diagnostics.Where(x => HasFix(provider, x.Id)).ToImmutableArray();
        }

        public ImmutableArray<Diagnostic> MatchingDiagnostics { get; }
        public CodeFixProvider CodeFixProvider { get; }
        public FixAllProvider FixAllProvider { get; }

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(MatchingDiagnostics);
        }

        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
            => Task.FromResult(MatchingDiagnostics.Where(x => string.Compare(x.Location.GetMappedLineSpan().Path, document.FilePath, true) == 0));

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<Diagnostic>>(MatchingDiagnostics);

        private static bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return codeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId);
        }

        public static AvailableFixAllDiagnosticProvider CreateOrDefault(CodeFixProvider provider, IEnumerable<Diagnostic> diagnostics)
        {
            if(provider.GetFixAllProvider() == default)
                return null;

            var result = new AvailableFixAllDiagnosticProvider(provider, diagnostics.Where(x => HasFix(provider, x.Id)));

            if(!result.MatchingDiagnostics.Any())
                return null;

            return result;
        }
    }
}