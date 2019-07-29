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

    public class AvailableFixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        private ImmutableArray<(DocumentId documentId, ProjectId projectId, Diagnostic diagnostic)> _fixableDiagnostics;

        protected AvailableFixAllDiagnosticProvider(CodeFixProvider provider, IEnumerable<DocumentDiagnostics> diagnostics)
        {
            CodeFixProvider = provider;
            FixAllProvider = provider.GetFixAllProvider();
            _fixableDiagnostics = diagnostics
                .SelectMany(x => x.Diagnostics, (parent, child) => (parent.DocumentId, parent.ProjectId, diagnostic: child))
                .Where(x => HasFix(provider, x.diagnostic.Id))
                .ToImmutableArray();
        }


        public CodeFixProvider CodeFixProvider { get; }
        public FixAllProvider FixAllProvider { get; }

        public ImmutableArray<(string id, string message)> GetAvailableFixableDiagnostics() => _fixableDiagnostics.Select(x => (x.diagnostic.Id, x.diagnostic.GetMessage())).ToImmutableArray();

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return Task.FromResult(_fixableDiagnostics.Where(x => x.projectId == project.Id).Select(x => x.diagnostic));
        }

        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            return Task.FromResult(_fixableDiagnostics.Where(x => x.documentId == document.Id).Select(x => x.diagnostic));
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return Task.FromResult(_fixableDiagnostics.Where(x => x.projectId == project.Id).Select(x => x.diagnostic));
        }

        private static bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return codeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId);
        }

        public static AvailableFixAllDiagnosticProvider CreateOrDefault(CodeFixProvider provider, IEnumerable<DocumentDiagnostics> diagnostics)
        {
            if(provider.GetFixAllProvider() == default)
                return null;

            var result = new AvailableFixAllDiagnosticProvider(provider, diagnostics);

            if(!result._fixableDiagnostics.Any())
                return null;

            return result;
        }
    }
}