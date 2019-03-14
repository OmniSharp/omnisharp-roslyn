using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.Extensions.Logging;
using OmniSharp.Helpers;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    public class CSharpDiagnosticWorkerWithAnalyzers : ICsDiagnosticWorker
    {
        private readonly AnalyzerWorkQueue _workQueue;
        private readonly ILogger<CSharpDiagnosticWorkerWithAnalyzers> _logger;
        private readonly ConcurrentDictionary<DocumentId, (string projectName, ImmutableArray<Diagnostic> diagnostics)> _currentDiagnosticResults =
            new ConcurrentDictionary<DocumentId, (string projectName, ImmutableArray<Diagnostic> diagnostics)>();
        private readonly ImmutableArray<ICodeActionProvider> _providers;
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmniSharpWorkspace _workspace;
        private readonly RulesetsForProjects _rulesetsForProjects;

        // This is workaround.
        // Currently roslyn doesn't expose official way to use IDE analyzers during analysis.
        // This options gives certain IDE analysis access for services that are not yet publicly available.
        private readonly ConstructorInfo _workspaceAnalyzerOptionsConstructor;
        private bool _initialSolutionAnalysisInvoked = false;

        public CSharpDiagnosticWorkerWithAnalyzers(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            DiagnosticEventForwarder forwarder,
            RulesetsForProjects rulesetsForProjects)
        {
            _logger = loggerFactory.CreateLogger<CSharpDiagnosticWorkerWithAnalyzers>();
            _providers = providers.ToImmutableArray();
            _workQueue = new AnalyzerWorkQueue(loggerFactory);

            _forwarder = forwarder;
            _workspace = workspace;
            _rulesetsForProjects = rulesetsForProjects;

            _workspaceAnalyzerOptionsConstructor = Assembly
                .Load("Microsoft.CodeAnalysis.Features")
                .GetType("Microsoft.CodeAnalysis.Diagnostics.WorkspaceAnalyzerOptions")
                .GetConstructor(new Type[] { typeof(AnalyzerOptions), typeof(OptionSet), typeof(Solution) })
                ?? throw new InvalidOperationException("Could not resolve 'Microsoft.CodeAnalysis.Diagnostics.WorkspaceAnalyzerOptions' for IDE analyzers.");

            _workspace.WorkspaceChanged += OnWorkspaceChanged;

            Task.Factory.StartNew(Worker, TaskCreationOptions.LongRunning);
        }

        private Task InitializeWithWorkspaceDocumentsIfNotYetDone()
        {
            if (_initialSolutionAnalysisInvoked)
                return Task.CompletedTask;

            _initialSolutionAnalysisInvoked = true;

            return Task.Run(async () =>
            {
                while (!_workspace.Initialized || _workspace.CurrentSolution.Projects.Count() == 0) await Task.Delay(50);
            })
            .ContinueWith(_ => Task.Delay(50))
            .ContinueWith(_ =>
            {
                var documentIds = QueueAllDocumentsForDiagnostics();
                _logger.LogInformation($"Solution initialized -> queue all documents for code analysis. Initial document count: {documentIds.Length}.");
            });
        }

        public ImmutableArray<DocumentId> QueueForDiagnosis(ImmutableArray<string> documentPaths)
        {
            var documentIds = GetDocumentIdsFromPaths(documentPaths);
            QueueForAnalysis(documentIds);
            return documentIds;
        }

        public async Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnostics(ImmutableArray<string> documentPaths)
        {
            await InitializeWithWorkspaceDocumentsIfNotYetDone();

            var documentIds = GetDocumentIdsFromPaths(documentPaths);

            return await GetDiagnosticsByDocumentIds(documentIds);
        }

        private async Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnosticsByDocumentIds(ImmutableArray<DocumentId> documentIds)
        {
            await _workQueue.WaitForResultsAsync(documentIds);

            return _currentDiagnosticResults
                .Where(x => documentIds.Any(docId => docId == x.Key))
                .SelectMany(x => x.Value.diagnostics, (k, v) => ((k.Value.projectName, v)))
                .ToImmutableArray();
        }

        private ImmutableArray<DocumentId> GetDocumentIdsFromPaths(ImmutableArray<string> documentPaths)
        {
            return documentPaths
                .Select(docPath => _workspace.GetDocumentId(docPath))
                .ToImmutableArray();
        }

        private async Task Worker()
        {
            while (true)
            {
                try
                {
                    var solution = _workspace.CurrentSolution;

                    var currentWorkGroupedByProjects = _workQueue
                        .TakeWork()
                        .Select(documentId => (projectId: solution.GetDocument(documentId)?.Project?.Id, documentId))
                        .Where(x => x.projectId != null)
                        .GroupBy(x => x.projectId, x => x.documentId)
                        .ToImmutableArray();

                    foreach (var projectGroup in currentWorkGroupedByProjects)
                    {
                        await AnalyzeProject(solution, projectGroup);
                    }

                    await Task.Delay(50);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Analyzer worker failed: {ex}");
                }
            }
        }

        private void QueueForAnalysis(ImmutableArray<DocumentId> documentIds)
        {
            foreach (var document in documentIds)
            {
                _workQueue.PutWork(document);
            }
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            if (changeEvent.Kind == WorkspaceChangeKind.DocumentChanged
                || changeEvent.Kind == WorkspaceChangeKind.DocumentAdded
                || changeEvent.Kind == WorkspaceChangeKind.DocumentReloaded
                || changeEvent.Kind == WorkspaceChangeKind.DocumentInfoChanged )
            {
                QueueForAnalysis(ImmutableArray.Create(changeEvent.DocumentId));
            }
            else if(changeEvent.Kind == WorkspaceChangeKind.DocumentRemoved)
            {
                _currentDiagnosticResults.TryRemove(changeEvent.DocumentId, out _);
            }
        }

        private async Task AnalyzeProject(Solution solution, IGrouping<ProjectId, DocumentId> documentsGroupedByProject)
        {
            try
            {
                // TODO: This should be moved that project rulesets are updated
                // to workspace projects itself when project is updated/loaded/manipulated and so on.
                // It also causes these inderictions and multiple steps to collect work with projects / documents.
                var projectOriginal = solution.GetProject(documentsGroupedByProject.Key);

                var projectWithOptions = projectOriginal.WithCompilationOptions(
                        _rulesetsForProjects.BuildCompilationOptionsWithCurrentRules(projectOriginal));

                var allAnalyzers = _providers
                    .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                    .Concat(projectWithOptions.AnalyzerReferences.SelectMany(x => x.GetAnalyzers(projectWithOptions.Language)))
                    .ToImmutableArray();

                var compiled = await projectWithOptions
                    .GetCompilationAsync();

                var workspaceAnalyzerOptions =
                    (AnalyzerOptions)_workspaceAnalyzerOptionsConstructor.Invoke(new object[] { projectWithOptions.AnalyzerOptions, projectWithOptions.Solution.Options, projectWithOptions.Solution });

                foreach (var documentId in documentsGroupedByProject)
                {
                    var document = projectWithOptions.GetDocument(documentId);
                    await AnalyzeDocument(projectWithOptions, allAnalyzers, compiled, workspaceAnalyzerOptions, document);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of project {documentsGroupedByProject.Key} failed, underlaying error: {ex}");
            }
        }

        private async Task AnalyzeDocument(Project project, ImmutableArray<DiagnosticAnalyzer> allAnalyzers, Compilation compiled, AnalyzerOptions workspaceAnalyzerOptions, Document document)
        {
            try
            {
                // There's real possibility that bug in analyzer causes analysis hang at document.
                var perDocumentTimeout = new CancellationTokenSource(10 * 1000);

                var documentSemanticModel = await document.GetSemanticModelAsync(perDocumentTimeout.Token);

                var diagnostics = ImmutableArray<Diagnostic>.Empty;

                // Only basic syntax check is available if file is miscellanous like orphan .cs file.
                // Those projects are on hard coded virtual project named 'MiscellaneousFiles.csproj'.
                if (project.Name == "MiscellaneousFiles.csproj")
                {
                    var syntaxTree = await document.GetSyntaxTreeAsync();
                    diagnostics = syntaxTree.GetDiagnostics().ToImmutableArray();
                }
                else if (allAnalyzers.Any()) // Analyzers cannot be called with empty analyzer list.
                {
                    var semanticDiagnosticsWithAnalyzers = await compiled
                        .WithAnalyzers(allAnalyzers, workspaceAnalyzerOptions)
                        .GetAnalyzerSemanticDiagnosticsAsync(documentSemanticModel, filterSpan: null, perDocumentTimeout.Token);

                    var syntaxDiagnosticsWithAnalyzers = await compiled
                        .WithAnalyzers(allAnalyzers, workspaceAnalyzerOptions)
                        .GetAnalyzerSyntaxDiagnosticsAsync(documentSemanticModel.SyntaxTree, perDocumentTimeout.Token);

                    diagnostics = semanticDiagnosticsWithAnalyzers
                        .Concat(syntaxDiagnosticsWithAnalyzers)
                        .Concat(documentSemanticModel.GetDiagnostics())
                        .ToImmutableArray();
                }
                else
                {
                    diagnostics = documentSemanticModel.GetDiagnostics().ToImmutableArray();
                }

                UpdateCurrentDiagnostics(project, document, diagnostics);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of document {document.Name} failed or cancelled by timeout: {ex.Message}, analysers: {string.Join(", ", allAnalyzers)}");
                _workQueue.MarkWorkAsCompleteForDocumentId(document.Id);
            }
        }

        private void UpdateCurrentDiagnostics(Project project, Document document, ImmutableArray<Diagnostic> diagnosticsWithAnalyzers)
        {
            _currentDiagnosticResults[document.Id] = (project.Name, diagnosticsWithAnalyzers);
            _workQueue.MarkWorkAsCompleteForDocumentId(document.Id);
            EmitDiagnostics(_currentDiagnosticResults[document.Id].diagnostics);
        }

        private void EmitDiagnostics(ImmutableArray<Diagnostic> results)
        {
            if (results.Any())
            {
                _forwarder.Forward(new DiagnosticMessage
                {
                    Results = results
                        .Select(x => x.ToDiagnosticLocation())
                        .Where(x => x.FileName != null)
                        .GroupBy(x => x.FileName)
                        .Select(group => new DiagnosticResult { FileName = group.Key, QuickFixes = group.ToList() })
                });
            }
        }

        public ImmutableArray<DocumentId> QueueAllDocumentsForDiagnostics()
        {
            var documentIds = _workspace.CurrentSolution.Projects.SelectMany(x => x.DocumentIds).ToImmutableArray();
            QueueForAnalysis(documentIds);
            return documentIds;
        }

        public async Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetAllDiagnosticsAsync()
        {
            await InitializeWithWorkspaceDocumentsIfNotYetDone();
            var allDocumentsIds = _workspace.CurrentSolution.Projects.SelectMany(x => x.DocumentIds).ToImmutableArray();
            return await GetDiagnosticsByDocumentIds(allDocumentsIds);
        }
    }
}
