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
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{

    [OmniSharpHandler(OmniSharpEndpoints.RunFixAll, LanguageNames.CSharp)]
    public class RunFixAllCodeActionService : FixAllCodeActionBase, IRequestHandler<RunFixAllRequest, RunFixAllResponse>
    {
        private readonly ICsDiagnosticWorker diagnosticWorker;
        private readonly OmniSharpWorkspace workspace;

        [ImportingConstructor]
        public RunFixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers) : base(diagnosticWorker, codeFixProvider, workspace, providers)
        {
            this.diagnosticWorker = diagnosticWorker;
            this.workspace = workspace;
        }

        public async Task<RunFixAllResponse> FixAll()
        {
            var solutionBeforeChanges = Workspace.CurrentSolution;
            var projectIds = solutionBeforeChanges.Projects.Select(x => x.Id);

            var availableCodeFixesLookup = projectIds.Select(x => (projectId: x, fixes: GetAvailableCodeFixes2(x))).ToDictionary(x => x.projectId, x => x.fixes);

            var allDiagnostics = await diagnosticWorker.GetAllDiagnosticsAsync();

            var mappedProvidersWithDiagnostics = allDiagnostics
                .SelectMany(diagnosticsInDocument =>
                    AvailableFixAllDiagnosticProvider.Create(availableCodeFixesLookup[diagnosticsInDocument.ProjectId], diagnosticsInDocument),
                    (parent, child) => (diagnosticsInDocument: parent, provider: child));

            foreach (var (diagnosticsInDocument, provider) in mappedProvidersWithDiagnostics)
            {
                try
                {
                    var document = workspace.CurrentSolution.GetDocument(diagnosticsInDocument.DocumentId);

                    var fixableIds = provider.GetAvailableFixableDiagnostics().Select(x => x.id);

                    var fixAllContext = new FixAllContext(
                        document,
                        provider.CodeFixProvider,
                        Microsoft.CodeAnalysis.CodeFixes.FixAllScope.Document,
                        string.Join("_", fixableIds),
                        fixableIds,
                        new FixAllDiagnosticProvider(diagnosticWorker),
                        CancellationToken.None
                    );

                    var fixes = await provider.FixAllProvider.GetFixAsync(fixAllContext);

                    if (fixes == default)
                        continue;

                    // if (provider.FixAllProvider.GetSupportedFixAllScopes().All(x => x != Microsoft.CodeAnalysis.CodeFixes.FixAllScope.Document))
                    //     throw new InvalidOperationException($"Not supported? {provider.FixAllProvider}");

                    var operations = await fixes.GetOperationsAsync(CancellationToken.None);

                    foreach (var o in operations)
                    {
                        if (o is ApplyChangesOperation applyChangesOperation)
                        {
                            applyChangesOperation.Apply(Workspace, CancellationToken.None);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            var currentSolution = Workspace.CurrentSolution;

            var getChangedDocumentIds = solutionBeforeChanges.GetChanges(currentSolution).GetProjectChanges().SelectMany(x => x.GetChangedDocuments());

            var changes = await Task.WhenAll(getChangedDocumentIds
                .Select(async x => (changes: await TextChanges.GetAsync(currentSolution.GetDocument(x), solutionBeforeChanges.GetDocument(x)), document: currentSolution.GetDocument(x))));

            return new RunFixAllResponse
            {
                Changes = changes.Select(x => new ModifiedFileResponse(x.document.FilePath) { Changes = x.changes.ToList() }).ToList()
            };
        }

        public async Task<RunFixAllResponse> Handle(RunFixAllRequest request)
        {
            return await FixAll();
        }

        private class FixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
        {
            private readonly ICsDiagnosticWorker _diagnosticWorker;

            public FixAllDiagnosticProvider(ICsDiagnosticWorker diagnosticWorker)
            {
                _diagnosticWorker = diagnosticWorker;
            }

            public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                var diagnostics = await _diagnosticWorker.GetDiagnostics(project.Documents.Select(x => x.FilePath).ToImmutableArray());
                return diagnostics.SelectMany(x => x.Diagnostics);
            }

            public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
            {
                var documentDiagnostics = await _diagnosticWorker.GetDiagnostics(ImmutableArray.Create(document.FilePath));

                if(!documentDiagnostics.Any())
                    return new Diagnostic[] {};

                return documentDiagnostics.First().Diagnostics;
            }

            public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
