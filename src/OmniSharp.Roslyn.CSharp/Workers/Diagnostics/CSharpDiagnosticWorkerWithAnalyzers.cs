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
using OmniSharp.Models.Events;
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
        private readonly OmniSharpOptions _options;
        private readonly OmniSharpWorkspace _workspace;

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
            OmniSharpOptions options)
        {
            _logger = loggerFactory.CreateLogger<CSharpDiagnosticWorkerWithAnalyzers>();
            _providers = providers.ToImmutableArray();
            _workQueue = new AnalyzerWorkQueue(loggerFactory, timeoutForPendingWorkMs: options.RoslynExtensionsOptions.DocumentAnalysisTimeoutMs * 3);

            _forwarder = forwarder;
            _options = options;
            _workspace = workspace;

            _workspaceAnalyzerOptionsConstructor = Assembly
                .Load("Microsoft.CodeAnalysis.Features")
                .GetType("Microsoft.CodeAnalysis.Diagnostics.WorkspaceAnalyzerOptions")
                .GetConstructor(new Type[] { typeof(AnalyzerOptions), typeof(Solution) })
                ?? throw new InvalidOperationException("Could not resolve 'Microsoft.CodeAnalysis.Diagnostics.WorkspaceAnalyzerOptions' for IDE analyzers.");

            _workspace.WorkspaceChanged += OnWorkspaceChanged;

            Task.Factory.StartNew(() => Worker(AnalyzerWorkType.Foreground), TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => Worker(AnalyzerWorkType.Background), TaskCreationOptions.LongRunning);
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
                var documentIds = QueueDocumentsForDiagnostics();
                _logger.LogInformation($"Solution initialized -> queue all documents for code analysis. Initial document count: {documentIds.Length}.");
            });
        }

        public async Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnostics(ImmutableArray<string> documentPaths)
        {
            await InitializeWithWorkspaceDocumentsIfNotYetDone();

            var documentIds = GetDocumentIdsFromPaths(documentPaths);

            return await GetDiagnosticsByDocumentIds(documentIds);
        }

        private async Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnosticsByDocumentIds(ImmutableArray<DocumentId> documentIds)
        {
            if(documentIds.Length == 1)
            {
                _workQueue.TryPromote(documentIds.Single());
                await _workQueue.WaitForegroundWorkComplete();
            }
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

        private async Task Worker(AnalyzerWorkType workType)
        {
            while (true)
            {
                try
                {
                    var solution = _workspace.CurrentSolution;

                    var currentWorkGroupedByProjects = _workQueue
                        .TakeWork(workType)
                        .Select(documentId => (projectId: solution.GetDocument(documentId)?.Project?.Id, documentId))
                        .Where(x => x.projectId != null)
                        .GroupBy(x => x.projectId, x => x.documentId)
                        .ToImmutableArray();

                    foreach (var projectGroup in currentWorkGroupedByProjects)
                    {
                        var projectPath = solution.GetProject(projectGroup.Key).FilePath;

                        EventIfBackgroundWork(workType, projectPath, ProjectDiagnosticStatus.Started);

                        await AnalyzeProject(solution, projectGroup);

                        EventIfBackgroundWork(workType, projectPath, ProjectDiagnosticStatus.Ready);
                    }

                    _workQueue.WorkComplete(workType);

                    await Task.Delay(50);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Analyzer worker failed: {ex}");
                }
            }
        }

        private void EventIfBackgroundWork(AnalyzerWorkType workType, string projectPath, ProjectDiagnosticStatus status)
        {
            if (workType == AnalyzerWorkType.Background)
                _forwarder.ProjectAnalyzedInBackground(projectPath, status);
        }

        private void QueueForAnalysis(ImmutableArray<DocumentId> documentIds, AnalyzerWorkType workType)
        {
            _workQueue.PutWork(documentIds, workType);
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            switch(changeEvent.Kind)
            {
                case WorkspaceChangeKind.DocumentChanged:
                case WorkspaceChangeKind.DocumentAdded:
                case WorkspaceChangeKind.DocumentReloaded:
                case WorkspaceChangeKind.DocumentInfoChanged:
                    QueueForAnalysis(ImmutableArray.Create(changeEvent.DocumentId), AnalyzerWorkType.Foreground);
                    break;
                case WorkspaceChangeKind.DocumentRemoved:
                    if(!_currentDiagnosticResults.TryRemove(changeEvent.DocumentId, out _))
                    {
                        _logger.LogDebug($"Tried to remove non existent document from analysis, document: {changeEvent.DocumentId}");
                    };
                    break;
                case WorkspaceChangeKind.ProjectAdded:
                case WorkspaceChangeKind.ProjectChanged:
                case WorkspaceChangeKind.ProjectReloaded:
                    _logger.LogDebug($"Project {changeEvent.ProjectId} updated, reanalyzing its diagnostics.");
                    var projectDocumentIds = _workspace.CurrentSolution.GetProject(changeEvent.ProjectId).Documents.Select(x => x.Id).ToImmutableArray();
                    QueueForAnalysis(projectDocumentIds, AnalyzerWorkType.Background);
                    break;
            }
        }

        private async Task AnalyzeProject(Solution solution, IGrouping<ProjectId, DocumentId> documentsGroupedByProject)
        {
            try
            {
                var project = solution.GetProject(documentsGroupedByProject.Key);

                var allAnalyzers = _providers
                    .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                    .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzers(project.Language)))
                    .ToImmutableArray();

                var compiled = await project
                    .GetCompilationAsync();

                var workspaceAnalyzerOptions =
                    (AnalyzerOptions)_workspaceAnalyzerOptionsConstructor.Invoke(new object[] { project.AnalyzerOptions, project.Solution });

                foreach (var documentId in documentsGroupedByProject)
                {
                    var document = project.GetDocument(documentId);
                    await AnalyzeDocument(project, allAnalyzers, compiled, workspaceAnalyzerOptions, document);
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
                var perDocumentTimeout = new CancellationTokenSource(_options.RoslynExtensionsOptions.DocumentAnalysisTimeoutMs);

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
                    var compilationWithAnalyzers = compiled.WithAnalyzers(allAnalyzers, new CompilationWithAnalyzersOptions(workspaceAnalyzerOptions,
                        onAnalyzerException: OnAnalyzerException,
                        concurrentAnalysis: false,
                        logAnalyzerExecutionTime: false,
                        reportSuppressedDiagnostics: false));

                    var semanticDiagnosticsWithAnalyzers = await compilationWithAnalyzers
                        .GetAnalyzerSemanticDiagnosticsAsync(documentSemanticModel, filterSpan: null, perDocumentTimeout.Token);

                    var syntaxDiagnosticsWithAnalyzers = await compilationWithAnalyzers
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
            }
        }

        private void OnAnalyzerException(Exception ex, DiagnosticAnalyzer analyzer, Diagnostic diagnostic)
        {
            _logger.LogDebug($"Exception in diagnostic analyzer." +
                $"\n            analyzer: {analyzer}" +
                $"\n            diagnostic: {diagnostic}" +
                $"\n            exception: {ex.Message}");
        }

        private void UpdateCurrentDiagnostics(Project project, Document document, ImmutableArray<Diagnostic> diagnosticsWithAnalyzers)
        {
            _currentDiagnosticResults[document.Id] = (project.Name, diagnosticsWithAnalyzers);
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

        public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics()
        {
            var documentIds = _workspace.CurrentSolution.Projects.SelectMany(x => x.DocumentIds).ToImmutableArray();
            QueueForAnalysis(documentIds, AnalyzerWorkType.Background);
            return documentIds;
        }

        public async Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetAllDiagnosticsAsync()
        {
            await InitializeWithWorkspaceDocumentsIfNotYetDone();
            var allDocumentsIds = _workspace.CurrentSolution.Projects.SelectMany(x => x.DocumentIds).ToImmutableArray();
            return await GetDiagnosticsByDocumentIds(allDocumentsIds);
        }

        public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(ImmutableArray<ProjectId> projectIds)
        {
            var documentIds = projectIds.SelectMany(projectId => _workspace.CurrentSolution.GetProject(projectId).Documents.Select(x => x.Id)).ToImmutableArray();
            QueueForAnalysis(documentIds, AnalyzerWorkType.Background);
            return documentIds;
        }
    }
}
