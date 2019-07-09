using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    public class FixAllCodeActionService
    {
        private readonly ICsDiagnosticWorker _diagnosticWorker;
        private readonly CachingCodeFixProviderForProjects _codeFixProvider;
        private readonly OmniSharpWorkspace _workspace;

        public FixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace)
        {
            _diagnosticWorker = diagnosticWorker;
            _codeFixProvider = codeFixProvider;
            _workspace = workspace;
        }

        public async Task FixAll()
        {
            // https://csharp.hotexamples.com/examples/-/FixAllContext/-/php-fixallcontext-class-examples.html

            var diagnostics = await _diagnosticWorker.GetAllDiagnosticsAsync();

            var provider = WellKnownFixAllProviders.BatchFixer;

            var solution = _workspace.CurrentSolution;
            var projects = _workspace.CurrentSolution.Projects;

            foreach (var project in projects)
            {
                var allCodefixesForProject = _codeFixProvider.GetAllCodeFixesForProject(project.Id);

                foreach (var codeFix in allCodefixesForProject.Where(x => x.GetFixAllProvider() != null))
                {
                    var matchingDiagnostics = diagnostics.Where(x => codeFix.FixableDiagnosticIds.Any(id => id == x.diagnostic.Id));

                    var fixAllContext = new FixAllContext(
                        project,
                        codeFix,
                        FixAllScope.Project,
                        matchingDiagnostics.First().diagnostic.Id,
                        matchingDiagnostics.Select(x => x.diagnostic.Id),
                        new FixAllDiagnosticProvider(matchingDiagnostics.Select(x => x.diagnostic).ToImmutableArray()),
                        CancellationToken.None);

                    var fixes = await provider.GetFixAsync(fixAllContext);

                    var operations = await fixes.GetOperationsAsync(CancellationToken.None);

                    foreach (var o in operations)
                    {
                        if (o is ApplyChangesOperation applyChangesOperation)
                        {
                            solution = applyChangesOperation.ChangedSolution;
                            _workspace.TryApplyChanges(solution);
                        }
                    }
                }
            }
        }

        private bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return codeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId);
        }

        private class FixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
        {
            private readonly ImmutableArray<Diagnostic> _diagnostics;

            public FixAllDiagnosticProvider(ImmutableArray<Diagnostic> diagnostics)
            {
                _diagnostics = diagnostics;
            }

            public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                return Task.FromResult<IEnumerable<Diagnostic>>(_diagnostics);
            }

            public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
                => Task.FromResult<IEnumerable<Diagnostic>>(_diagnostics);

            public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
                => Task.FromResult<IEnumerable<Diagnostic>>(_diagnostics);
        }
    }
}