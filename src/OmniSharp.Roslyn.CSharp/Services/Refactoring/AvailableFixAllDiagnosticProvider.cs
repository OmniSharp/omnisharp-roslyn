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
        public AvailableFixAllDiagnosticProvider(CodeFixProvider provider, IEnumerable<Diagnostic> matchingDiagnostics)
        {
            CodeFixProvider = provider;
            MatchingDiagnostics = matchingDiagnostics.ToImmutableArray();
            FixAllProvider = provider.GetFixAllProvider() ?? throw new InvalidOperationException($"{nameof(AvailableFixAllDiagnosticProvider)} should not be constructed without {nameof(provider.GetFixAllProvider)}");
        }

        public ImmutableArray<Diagnostic> MatchingDiagnostics { get; }
        public CodeFixProvider CodeFixProvider { get; }
        public FixAllProvider FixAllProvider { get; }

        // TODO: Provider should only return diagnostics from correct context.
        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(MatchingDiagnostics);
        }

        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
            => Task.FromResult(MatchingDiagnostics.Where(x => string.Compare(x.Location.GetMappedLineSpan().Path, document.FilePath, true) == 0));

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<Diagnostic>>(MatchingDiagnostics);
    }
}