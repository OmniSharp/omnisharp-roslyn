using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{

    public class AvailableFixAllDiagnosticProvider // : FixAllContext.DiagnosticProvider
    {
        private readonly DocumentDiagnostics _documentDiagnostics;

        protected AvailableFixAllDiagnosticProvider(CodeFixProvider provider, DocumentDiagnostics documentDiagnostics)
        {
            CodeFixProvider = provider;
            _documentDiagnostics = documentDiagnostics;
            FixAllProvider = provider.GetFixAllProvider();
        }

        public CodeFixProvider CodeFixProvider { get; }
        public FixAllProvider FixAllProvider { get; }

        public ImmutableArray<(string id, string message)> GetAvailableFixableDiagnostics() => _documentDiagnostics.Diagnostics.Where(x => HasFix(CodeFixProvider, x.Id)).Select(x => (x.Id, x.GetMessage())).ToImmutableArray();

        // public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        // {
        //     return Task.FromResult<IEnumerable<Diagnostic>>(_documentDiagnostics.Diagnostics);
        // }

        // public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        // {
        //     if(document.Id != _documentDiagnostics.DocumentId)
        //     {
        //     }

        //     return Task.FromResult(_documentDiagnostics.Diagnostics.Where(x => x.documentId == document.Id).Select(x => x.diagnostic));
        // }

        // public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        // {
        //     throw new NotImplementedException();
        // }

        private static bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return codeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId);
        }

        public static AvailableFixAllDiagnosticProvider CreateOrDefault(CodeFixProvider provider, DocumentDiagnostics diagnostics)
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