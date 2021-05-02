using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.Extensions.Logging;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Mef;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;
using FixAllScope = OmniSharp.Abstractions.Models.V1.FixAll.FixAllScope;
using RoslynFixAllScope = Microsoft.CodeAnalysis.CodeFixes.FixAllScope;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.RunFixAll, LanguageNames.CSharp)]
    public class RunFixAllCodeActionService : BaseCodeActionService<RunFixAllRequest, RunFixAllResponse>
    {
        private readonly ILogger<RunFixAllCodeActionService> _logger;
        private readonly FixAllDiagnosticProvider _fixAllDiagnosticProvider;

        [ImportingConstructor]
        public RunFixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            CachingCodeFixProviderForProjects codeFixProvider,
            OmniSharpWorkspace workspace,
            ILoggerFactory loggerFactory) :
            base(
                workspace,
                providers,
                loggerFactory.CreateLogger<RunFixAllCodeActionService>(),
                diagnosticWorker,
                codeFixProvider)
        {
            _logger = loggerFactory.CreateLogger<RunFixAllCodeActionService>();
            _fixAllDiagnosticProvider = new FixAllDiagnosticProvider(diagnosticWorker);
        }

        public async override Task<RunFixAllResponse> Handle(RunFixAllRequest request)
        {
            var solutionBeforeChanges = Workspace.CurrentSolution;
            if (!(Workspace.GetDocument(request.FileName) is Document document))
            {
                _logger.LogWarning("Requested fix all for document {0} that does not exist!", request.FileName);
                return new RunFixAllResponse();
            }

            var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.Timeout));
            switch (request)
            {
                case { Scope: FixAllScope.Document, FixAllFilter: null }:
                    var allDiagnosticsInFile = await GetDiagnosticsAsync(FixAllScope.Document, document);
                    if (allDiagnosticsInFile.IsDefaultOrEmpty)
                    {
                        break;
                    }

                    if (allDiagnosticsInFile.Length > 1)
                    {
                        Debug.Fail("Not expecting to get back more documents than were passed in");
                        break;
                    }

                    _logger.LogInformation("Found {0} diagnostics to fix.", allDiagnosticsInFile[0].Diagnostics.Length);
                    foreach (var diagnostic in allDiagnosticsInFile[0].Diagnostics)
                    {
                        document = await FixSpecificDiagnosticIdAsync(document, diagnostic.Id, FixAllScope.Document, CancellationToken.None);
                    }

                    break;

                case { FixAllFilter: { } filters }:
                    foreach (var filter in filters)
                    {
                        document = await FixSpecificDiagnosticIdAsync(document, filter.Id, request.Scope, cancellationSource.Token);
                    }

                    break;

                default:
                    throw new NotImplementedException($"Only scope '{nameof(FixAllScope.Document)}' is currently supported when filter '{nameof(request.FixAllFilter)}' is not set.");
            }

            var solutionAfterChanges = document.Project.Solution;
            if (request.ApplyChanges != false)
            {
                _logger.LogInformation("Applying changes from the fixers.");
                Workspace.TryApplyChanges(solutionAfterChanges);
            }

            var changes = await GetFileChangesAsync(document.Project.Solution, solutionBeforeChanges, Path.GetDirectoryName(request.FileName), request.WantsTextChanges, request.WantsAllCodeActionOperations);

            return new RunFixAllResponse
            {
                Changes = changes.FileChanges
            };
        }

        private async Task<Document> FixSpecificDiagnosticIdAsync(Document document, string diagnosticId, FixAllScope scope, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fixing {0}.", diagnosticId);
            var originalDoc = document;
            var codeFixProvider = GetCodeFixProviderForId(document, diagnosticId);
            if (codeFixProvider is null || !(codeFixProvider.GetFixAllProvider() is FixAllProvider fixAllProvider))
            {
                _logger.LogInformation("Could not find a codefix provider or a fixall provider for {0}.", diagnosticId);
                return originalDoc;
            }

            _logger.LogTrace("Determing if {0} is still present in the document.", diagnosticId);
            var (diagnosticDocId, primaryDiagnostic) = await GetDocumentIdAndDiagnosticForGivenId(scope, document, diagnosticId);
            cancellationToken.ThrowIfCancellationRequested();
            if (primaryDiagnostic is null)
            {
                _logger.LogInformation("No diagnostic locations found for {0}.", diagnosticId);
                return originalDoc;
            }

            if (document.Id != diagnosticDocId)
            {
                document = document.Project.Solution.GetDocument(diagnosticDocId);
                if (document is null)
                {
                    throw new InvalidOperationException("Could not find the document with the diagnostic in the solution");
                }
            }

            _logger.LogTrace("{0} is still present in the document. Getting fixes.", diagnosticId);
            CodeAction action = null;
            var context = new CodeFixContext(document, primaryDiagnostic,
                (a, _) =>
                {
                    if (action == null)
                    {
                        action = a;
                    }
                },
                cancellationToken);

            await codeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

            var roslynScope = scope switch
            {
                FixAllScope.Document => RoslynFixAllScope.Document,
                FixAllScope.Project => RoslynFixAllScope.Project,
                FixAllScope.Solution => RoslynFixAllScope.Solution,
                _ => throw new InvalidOperationException()
            };

            var fixAllContext = new FixAllContext(document, codeFixProvider, roslynScope, action.EquivalenceKey, ImmutableArray.Create(diagnosticId), _fixAllDiagnosticProvider, cancellationToken);

            _logger.LogTrace("Finding FixAll fix for {0}.", diagnosticId);
            var fixes = await fixAllProvider.GetFixAsync(fixAllContext);
            if (fixes == null)
            {
                _logger.LogInformation("FixAll not found for {0}.", diagnosticId);
                return originalDoc;
            }

            _logger.LogTrace("Getting FixAll operations for {0}.", diagnosticId);
            var operations = await fixes.GetOperationsAsync(cancellationToken);

            // Currently, there are no roslyn changes that will result in multiple ApplyChangesOperations
            Debug.Assert(operations.OfType<ApplyChangesOperation>().Count() < 2);
            if (operations.OfType<ApplyChangesOperation>().FirstOrDefault() is ApplyChangesOperation applyChangesOperation)
            {
                _logger.LogTrace("Found apply changes operation for {0}.", diagnosticId);
                return applyChangesOperation.ChangedSolution.GetDocument(originalDoc.Id);
            }
            else
            {
                _logger.LogTrace("No apply changes operation for {0}.", diagnosticId);
                return originalDoc;
            }
        }

        private class FixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
        {
            private readonly ICsDiagnosticWorker _diagnosticWorker;

            public FixAllDiagnosticProvider(ICsDiagnosticWorker diagnosticWorker)
            {
                _diagnosticWorker = diagnosticWorker;
            }

            public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
                => await _diagnosticWorker.AnalyzeProjectsAsync(project, cancellationToken);

            public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
                => await _diagnosticWorker.AnalyzeDocumentAsync(document, cancellationToken);

            public override async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
                => await _diagnosticWorker.AnalyzeProjectsAsync(project, cancellationToken);
        }
    }
}
