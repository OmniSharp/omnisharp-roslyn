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
using Microsoft.Extensions.Logging;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Roslyn.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{

    [OmniSharpHandler(OmniSharpEndpoints.RunFixAll, LanguageNames.CSharp)]
    public class RunFixAllCodeActionService : FixAllCodeActionBase, IRequestHandler<RunFixAllRequest, RunFixAllResponse>
    {
        private readonly ILogger<RunFixAllCodeActionService> _logger;
        private readonly FixAllDiagnosticProvider _fixAllDiagnosticProvider;

        [ImportingConstructor]
        public RunFixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace, ILoggerFactory loggerFactory) : base(diagnosticWorker, codeFixProvider, workspace)
        {
            _logger = loggerFactory.CreateLogger<RunFixAllCodeActionService>();
            _fixAllDiagnosticProvider = new FixAllDiagnosticProvider(diagnosticWorker);
        }

        public async Task<RunFixAllResponse> Handle(RunFixAllRequest request)
        {
            var solutionBeforeChanges = Workspace.CurrentSolution;

            var mappedProvidersWithDiagnostics = await GetDiagnosticsMappedWithFixAllProviders();

            var filteredDiagnosticsWithFix = mappedProvidersWithDiagnostics
                .Where(x => IsFixOnScope(x, request.Scope, request.FileName))
                .Where(diagWithFix =>
                {
                    if(request.FixAllFilter == default)
                        return true;

                    return ContainsMatching(diagWithFix.GetAvailableFixableDiagnostics().Select(x => x.id), request.FixAllFilter.Select(x => x.Id));
                });

            foreach (var diagnosticsInDocument in filteredDiagnosticsWithFix)
            {
                try
                {
                    var document = Workspace.CurrentSolution.GetDocument(diagnosticsInDocument.DocumentId);

                    var fixableIds = diagnosticsInDocument.GetAvailableFixableDiagnostics().Select(x => x.id);

                    var fixAllContext = new FixAllContext(
                        document,
                        diagnosticsInDocument.CodeFixProvider,
                        Microsoft.CodeAnalysis.CodeFixes.FixAllScope.Document,
                        string.Join("_", fixableIds),
                        fixableIds,
                        _fixAllDiagnosticProvider,
                        CancellationToken.None
                    );

                    var fixes = await diagnosticsInDocument.FixAllProvider.GetFixAsync(fixAllContext);

                    if (fixes == default)
                        continue;

                    var operations = await fixes.GetOperationsAsync(CancellationToken.None);

                    foreach (var o in operations)
                    {
                        _logger.LogInformation($"Applying operation {o.ToString()} from fix all with fix provider {diagnosticsInDocument.CodeFixProvider} to workspace document {document.FilePath}.");

                        if (o is ApplyChangesOperation applyChangesOperation)
                        {
                            applyChangesOperation.Apply(Workspace, CancellationToken.None);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Running fix all action {diagnosticsInDocument} in document {diagnosticsInDocument.DocumentPath} prevented by error: {ex}");
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

        public static bool ContainsMatching(IEnumerable<string> listA, IEnumerable<string> listB)
        {
            return listA.Any(x => listB.Contains(x));
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
