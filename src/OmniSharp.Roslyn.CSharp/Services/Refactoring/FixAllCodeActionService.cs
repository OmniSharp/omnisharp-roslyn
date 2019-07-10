using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.FixAll, LanguageNames.CSharp)]
    public class FixAllCodeActionService: IRequestHandler<FixAllRequest, FixAllResponse>
    {
        private readonly ICsDiagnosticWorker _diagnosticWorker;
        private readonly CachingCodeFixProviderForProjects _codeFixProvider;
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _providers;

        [ImportingConstructor]
        public FixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers)
        {
            _diagnosticWorker = diagnosticWorker;
            _codeFixProvider = codeFixProvider;
            _workspace = workspace;
            this._providers = providers;
        }

        public async Task<FixAllResponse> FixAll()
        {
            // https://csharp.hotexamples.com/examples/-/FixAllContext/-/php-fixallcontext-class-examples.html

            var diagnostics = await _diagnosticWorker.GetAllDiagnosticsAsync();

            var solution = _workspace.CurrentSolution;
            var projects = _workspace.CurrentSolution.Projects;
            var changes = new List<SolutionChanges>();

            foreach (var project in projects)
            {
                var allCodefixesForProject =
                    _providers.SelectMany(provider => provider.CodeFixProviders)
                        .Concat(_codeFixProvider.GetAllCodeFixesForProject(project.Id));

                foreach (var codeFix in allCodefixesForProject.Where(x => x.GetFixAllProvider() != null))
                {
                    var matchingDiagnostics = diagnostics.Where(x => codeFix.FixableDiagnosticIds.Any(id => id == x.diagnostic.Id)).GroupBy(x => x.diagnostic.Id).Select(x => x.First());

                    if (!matchingDiagnostics.Any())
                        continue;

                    var fixAllContext = new FixAllContext(
                        project.Documents.First(),
                        codeFix,
                        FixAllScope.Project,
                        matchingDiagnostics.First().diagnostic.Id,
                        matchingDiagnostics.Select(x => x.diagnostic.Id),
                        new FixAllDiagnosticProvider(matchingDiagnostics.Select(x => x.diagnostic).ToImmutableArray()),
                        CancellationToken.None);

                    var provider = codeFix.GetFixAllProvider();

                    var fixes = await provider.GetFixAsync(fixAllContext);

                    if (fixes == default)
                        continue;

                    var operations = await fixes.GetOperationsAsync(CancellationToken.None);

                    foreach (var o in operations)
                    {
                        Console.WriteLine($"Operation {o.Title}");

                        if (o is ApplyChangesOperation applyChangesOperation)
                        {
                            Console.WriteLine($"Applying {o.Title}");

                            solution = applyChangesOperation.ChangedSolution;

                            var newChanges = solution.GetChanges(_workspace.CurrentSolution);

                            if(_workspace.TryApplyChanges(solution))
                            {
                                Console.WriteLine("Adding changes.");
                                changes.Add(newChanges);
                            }
                        }
                    }
                }
            }

            return new FixAllResponse {
                Changes = changes.SelectMany(x => x.)
            }
        }

        public async Task<FixAllResponse> Handle(FixAllRequest request)
        {
            await FixAll();
            return new FixAllResponse();
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

    [OmniSharpEndpoint(OmniSharpEndpoints.FixAll, typeof(FixAllRequest), typeof(FixAllResponse))]
    public class FixAllRequest : SimpleFileRequest
    {
    }

    public class FixAllResponse: IAggregateResponse
    {
        public FixAllResponse()
        {
            Changes = new List<ModifiedFileResponse>();
        }

        public List<ModifiedFileResponse> Changes { get; set; }

        public IAggregateResponse Merge(IAggregateResponse response) { return response; }
    }
}
