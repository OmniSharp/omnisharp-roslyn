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
using FixAllScope = OmniSharp.Abstractions.Models.V1.FixAll.FixAllScope;

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
            if(request.Scope != FixAllScope.Document && request.FixAllFilter == null)
                throw new NotImplementedException($"Only scope '{nameof(FixAllScope.Document)}' is currently supported when filter '{nameof(request.FixAllFilter)}' is not set.");

            var solutionBeforeChanges = Workspace.CurrentSolution;

            var mappedProvidersWithDiagnostics = await GetDiagnosticsMappedWithFixAllProviders(request.Scope, request.FileName);

            var filteredProvidersWithFix = mappedProvidersWithDiagnostics
                .Where(diagWithFix =>
                {
                    if (request.FixAllFilter == null)
                        return true;

                    return request.FixAllFilter.Any(x => diagWithFix.HasFixForId(x.Id));
                });

            var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.Timeout));

            foreach (var singleFixableProviderWithDocument in filteredProvidersWithFix)
            {
                try
                {
                    var document = Workspace.CurrentSolution.GetDocument(singleFixableProviderWithDocument.DocumentId);

                    var fixer = singleFixableProviderWithDocument.FixAllProvider;

                    var (action, fixableDiagnosticIds) = await singleFixableProviderWithDocument.RegisterCodeFixesOrDefault(document);

                    if (action == null)
                        continue;

                    var fixAllContext = new FixAllContext(
                        document,
                        singleFixableProviderWithDocument.CodeFixProvider,
                        Microsoft.CodeAnalysis.CodeFixes.FixAllScope.Project,
                        action.EquivalenceKey,
                        fixableDiagnosticIds,
                        _fixAllDiagnosticProvider,
                        cancellationSource.Token
                    );

                    var fixes = await singleFixableProviderWithDocument.FixAllProvider.GetFixAsync(fixAllContext);

                    if (fixes == null)
                        continue;

                    var operations = await fixes.GetOperationsAsync(cancellationSource.Token);

                    foreach (var o in operations)
                    {
                        _logger.LogInformation($"Applying operation {o.ToString()} from fix all with fix provider {singleFixableProviderWithDocument.CodeFixProvider} to workspace document {document.FilePath}.");

                        if (o is ApplyChangesOperation applyChangesOperation)
                        {
                            applyChangesOperation.Apply(Workspace, cancellationSource.Token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Running fix all action {singleFixableProviderWithDocument} in document {singleFixableProviderWithDocument.DocumentPath} prevented by error: {ex}");
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

                if (!documentDiagnostics.Any())
                    return new Diagnostic[] { };

                return documentDiagnostics.First().Diagnostics;
            }

            public override async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                var diagnostics = await _diagnosticWorker.GetDiagnostics(project.Documents.Select(x => x.FilePath).ToImmutableArray());
                return diagnostics.SelectMany(x => x.Diagnostics);
            }
        }
    }
}
