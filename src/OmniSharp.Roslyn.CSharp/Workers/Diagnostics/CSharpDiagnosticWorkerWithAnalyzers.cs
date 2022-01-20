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
using Microsoft.Extensions.Logging;
using OmniSharp.Helpers;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.Events;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    public class CSharpDiagnosticWorkerWithAnalyzers : ICsDiagnosticWorker, IDisposable
    {
        private readonly AnalyzerWorkQueue _workQueue;
        private readonly SemaphoreSlim _throttler;
        private readonly ILogger<CSharpDiagnosticWorkerWithAnalyzers> _logger;

        private readonly ConcurrentDictionary<DocumentId, DocumentDiagnostics> _currentDiagnosticResultLookup = new();
        private readonly ImmutableArray<ICodeActionProvider> _providers;
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmniSharpOptions _options;
        private readonly OmniSharpWorkspace _workspace;

        // This is workaround.
        // Currently roslyn doesn't expose official way to use IDE analyzers during analysis.
        // This options gives certain IDE analysis access for services that are not yet publicly available.
        private readonly ConstructorInfo _workspaceAnalyzerOptionsConstructor;

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
            _throttler = new SemaphoreSlim(options.RoslynExtensionsOptions.DiagnosticWorkersThreadCount);

            _forwarder = forwarder;
            _options = options;
            _workspace = workspace;

            _workspaceAnalyzerOptionsConstructor = Assembly
                .Load("Microsoft.CodeAnalysis.Features")
                .GetType("Microsoft.CodeAnalysis.Diagnostics.WorkspaceAnalyzerOptions")
                .GetConstructor(new Type[] { typeof(AnalyzerOptions), typeof(Solution) })
                ?? throw new InvalidOperationException("Could not resolve 'Microsoft.CodeAnalysis.Diagnostics.WorkspaceAnalyzerOptions' for IDE analyzers.");

            _workspace.WorkspaceChanged += OnWorkspaceChanged;
            _workspace.OnInitialized += OnWorkspaceInitialized;

            Task.Factory.StartNew(() => Worker(AnalyzerWorkType.Foreground), TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => Worker(AnalyzerWorkType.Background), TaskCreationOptions.LongRunning);

            OnWorkspaceInitialized(_workspace.Initialized);
        }

        public void OnWorkspaceInitialized(bool isInitialized)
        {
            if (isInitialized)
            {
                var documentIds = QueueDocumentsForDiagnostics();
                _logger.LogInformation($"Solution initialized -> queue all documents for code analysis. Initial document count: {documentIds.Length}.");
            }
        }

        public async Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<string> documentPaths)
        {
            var documentIds = GetDocumentIdsFromPaths(documentPaths);

            return await GetDiagnosticsByDocumentIds(documentIds, waitForDocuments: true);
        }

        private async Task<ImmutableArray<DocumentDiagnostics>> GetDiagnosticsByDocumentIds(ImmutableArray<DocumentId> documentIds, bool waitForDocuments)
        {
            if (waitForDocuments)
            {
                foreach (var documentId in documentIds)
                {
                    _workQueue.TryPromote(documentId);
                }

                await _workQueue.WaitForegroundWorkComplete();
            }

            return documentIds
                .Where(x => _currentDiagnosticResultLookup.ContainsKey(x))
                .Select(x => _currentDiagnosticResultLookup[x])
                .ToImmutableArray();
        }

        private ImmutableArray<DocumentId> GetDocumentIdsFromPaths(ImmutableArray<string> documentPaths)
        {
            return documentPaths
                .Select(docPath => _workspace.GetDocumentId(docPath))
                .Where(x => x != default)
                .ToImmutableArray();
        }

        private async Task Worker(AnalyzerWorkType workType)
        {
            while (true)
            {
                try
                {
                    var solution = _workspace.CurrentSolution;

                    var documents = _workQueue
                        .TakeWork(workType)
                        .Select(documentId => (projectId: solution.GetDocument(documentId)?.Project?.Id, documentId))
                        .Where(x => x.projectId != null)
                        .ToImmutableArray();

                    var documentCount = documents.Length;
                    var documentCountRemaining = documentCount;

                    // event every percentage increase, or every 10th if there are fewer than 1000
                    var eventEvery = Math.Max(10, documentCount / 100);

                    var documentsGroupedByProjects = documents
                        .GroupBy(x => x.projectId, x => x.documentId)
                        .ToImmutableArray();
                    var projectCount = documentsGroupedByProjects.Length;

                    EventIfBackgroundWork(workType, BackgroundDiagnosticStatus.Started, projectCount, documentCount, documentCountRemaining);

                    void decrementDocumentCountRemaining()
                    {
                        var remaining = Interlocked.Decrement(ref documentCountRemaining);
                        var done = documentCount - remaining;
                        if (done % eventEvery == 0)
                        {
                            EventIfBackgroundWork(workType, BackgroundDiagnosticStatus.Progress, projectCount, documentCount, remaining);
                        }
                    }

                    try
                    {
                        var projectAnalyzerTasks =
                            documentsGroupedByProjects
                                .Select(projectGroup => Task.Run(async () =>
                                {
                                    var projectPath = solution.GetProject(projectGroup.Key).FilePath;
                                    await AnalyzeProject(solution, projectGroup, decrementDocumentCountRemaining);
                                }))
                                .ToImmutableArray();

                        await Task.WhenAll(projectAnalyzerTasks);
                    }
                    finally
                    {
                        EventIfBackgroundWork(workType, BackgroundDiagnosticStatus.Finished, projectCount, documentCount, documentCountRemaining);
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

        private void EventIfBackgroundWork(AnalyzerWorkType workType, BackgroundDiagnosticStatus status, int numberProjects, int numberFiles, int numberFilesRemaining)
        {
            if (workType == AnalyzerWorkType.Background)
                _forwarder.BackgroundDiagnosticsStatus(status, numberProjects, numberFiles, numberFilesRemaining);
        }

        private void QueueForAnalysis(ImmutableArray<DocumentId> documentIds, AnalyzerWorkType workType)
        {
            _workQueue.PutWork(documentIds, workType);
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            switch (changeEvent.Kind)
            {
                case WorkspaceChangeKind.DocumentChanged:
                case WorkspaceChangeKind.DocumentAdded:
                case WorkspaceChangeKind.DocumentReloaded:
                case WorkspaceChangeKind.DocumentInfoChanged:
                    QueueForAnalysis(ImmutableArray.Create(changeEvent.DocumentId), AnalyzerWorkType.Foreground);
                    break;
                case WorkspaceChangeKind.DocumentRemoved:
                    if (!_currentDiagnosticResultLookup.TryRemove(changeEvent.DocumentId, out _))
                    {
                        _logger.LogDebug($"Tried to remove non existent document from analysis, document: {changeEvent.DocumentId}");
                    }
                    break;
                case WorkspaceChangeKind.AnalyzerConfigDocumentChanged:
                    _logger.LogDebug($"Analyzer config document {changeEvent.DocumentId} changed, which triggered re-analysis of project {changeEvent.ProjectId}.");
                    QueueForAnalysis(_workspace.CurrentSolution.GetProject(changeEvent.ProjectId).Documents.Select(x => x.Id).ToImmutableArray(), AnalyzerWorkType.Background);
                    break;
                case WorkspaceChangeKind.ProjectAdded:
                case WorkspaceChangeKind.ProjectChanged:
                case WorkspaceChangeKind.ProjectReloaded:
                    _logger.LogDebug($"Project {changeEvent.ProjectId} updated, reanalyzing its diagnostics.");
                    QueueForAnalysis(_workspace.CurrentSolution.GetProject(changeEvent.ProjectId).Documents.Select(x => x.Id).ToImmutableArray(), AnalyzerWorkType.Background);
                    break;
                case WorkspaceChangeKind.SolutionAdded:
                case WorkspaceChangeKind.SolutionChanged:
                case WorkspaceChangeKind.SolutionReloaded:
                    QueueDocumentsForDiagnostics();
                    break;
            }
        }

        public async Task<IEnumerable<Diagnostic>> AnalyzeDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            Project project = document.Project;
            var allAnalyzers = GetAnalyzersForProject(project);
            var compilation = await project.GetCompilationAsync(cancellationToken);
            var workspaceAnalyzerOptions = (AnalyzerOptions)_workspaceAnalyzerOptionsConstructor.Invoke(new object[] { project.AnalyzerOptions, project.Solution });

            cancellationToken.ThrowIfCancellationRequested();
            return await AnalyzeDocument(project, allAnalyzers, compilation, workspaceAnalyzerOptions, document);
        }

        public async Task<IEnumerable<Diagnostic>> AnalyzeProjectsAsync(Project project, CancellationToken cancellationToken)
        {
            var allAnalyzers = GetAnalyzersForProject(project);
            var compilation = await project.GetCompilationAsync(cancellationToken);
            var workspaceAnalyzerOptions = (AnalyzerOptions)_workspaceAnalyzerOptionsConstructor.Invoke(new object[] { project.AnalyzerOptions, project.Solution });
            var documentAnalyzerTasks = new List<Task>();
            var diagnostics = ImmutableList<Diagnostic>.Empty;

            foreach (var document in project.Documents)
            {
                await _throttler.WaitAsync(cancellationToken);

                documentAnalyzerTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var documentDiagnostics = await AnalyzeDocument(project, allAnalyzers, compilation, workspaceAnalyzerOptions, document);
                        ImmutableInterlocked.Update(ref diagnostics, currentDiagnostics => currentDiagnostics.AddRange(documentDiagnostics));
                    }
                    finally
                    {
                        _throttler.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(documentAnalyzerTasks);

            return diagnostics;
        }

        private async Task AnalyzeProject(Solution solution, IGrouping<ProjectId, DocumentId> documentsGroupedByProject, Action decrementRemaining)
        {
            try
            {
                var project = solution.GetProject(documentsGroupedByProject.Key);
                var allAnalyzers = GetAnalyzersForProject(project);
                var compilation = await project.GetCompilationAsync();
                var workspaceAnalyzerOptions = (AnalyzerOptions)_workspaceAnalyzerOptionsConstructor.Invoke(new object[] { project.AnalyzerOptions, project.Solution });
                var documentAnalyzerTasks = new List<Task>();

                foreach (var documentId in documentsGroupedByProject)
                {
                    await _throttler.WaitAsync();

                    documentAnalyzerTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var document = project.GetDocument(documentId);
                            var diagnostics = await AnalyzeDocument(project, allAnalyzers, compilation, workspaceAnalyzerOptions, document);
                            UpdateCurrentDiagnostics(project, document, diagnostics);
                            decrementRemaining();
                        }
                        finally
                        {
                            _throttler.Release();
                        }
                    }));
                }

                await Task.WhenAll(documentAnalyzerTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of project {documentsGroupedByProject.Key} failed, underlaying error: {ex}");
            }
        }

        private async Task<ImmutableArray<Diagnostic>> AnalyzeDocument(Project project, ImmutableArray<DiagnosticAnalyzer> allAnalyzers, Compilation compilation, AnalyzerOptions workspaceAnalyzerOptions, Document document)
        {
            try
            {
                // There's real possibility that bug in analyzer causes analysis hang at document.
                var perDocumentTimeout =
                    new CancellationTokenSource(_options.RoslynExtensionsOptions.DocumentAnalysisTimeoutMs);

                var documentSemanticModel = await document.GetSemanticModelAsync(perDocumentTimeout.Token);

                // Only basic syntax check is available if file is miscellanous like orphan .cs file.
                // Those projects are on hard coded virtual project
                if (project.Name == $"{Configuration.OmniSharpMiscProjectName}.csproj")
                {
                    var syntaxTree = await document.GetSyntaxTreeAsync();
                    return syntaxTree.GetDiagnostics().ToImmutableArray();
                }
                else if (allAnalyzers.Any()) // Analyzers cannot be called with empty analyzer list.
                {
                    var compilationWithAnalyzers = compilation.WithAnalyzers(allAnalyzers, new CompilationWithAnalyzersOptions(
                        workspaceAnalyzerOptions,
                        onAnalyzerException: OnAnalyzerException,
                        concurrentAnalysis: false,
                        logAnalyzerExecutionTime: false,
                        reportSuppressedDiagnostics: false));

                    var semanticDiagnosticsWithAnalyzers = await compilationWithAnalyzers
                        .GetAnalyzerSemanticDiagnosticsAsync(documentSemanticModel, filterSpan: null, perDocumentTimeout.Token);

                    var syntaxDiagnosticsWithAnalyzers = await compilationWithAnalyzers
                        .GetAnalyzerSyntaxDiagnosticsAsync(documentSemanticModel.SyntaxTree, perDocumentTimeout.Token);

                    return semanticDiagnosticsWithAnalyzers
                        .Concat(syntaxDiagnosticsWithAnalyzers)
                        .Where(d => !d.IsSuppressed)
                        .Concat(documentSemanticModel.GetDiagnostics())
                        .ToImmutableArray();
                }
                else
                {
                    return documentSemanticModel.GetDiagnostics();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of document {document.Name} failed or cancelled by timeout: {ex.Message}, analysers: {string.Join(", ", allAnalyzers)}");
                return ImmutableArray<Diagnostic>.Empty;
            }
        }

        private ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForProject(Project project)
        {
            return _providers
                .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzers(project.Language)))
                .ToImmutableArray();
        }

        private void OnAnalyzerException(Exception ex, DiagnosticAnalyzer analyzer, Diagnostic diagnostic)
        {
            _logger.LogDebug("Exception in diagnostic analyzer." +
                $"\n            analyzer: {analyzer}" +
                $"\n            diagnostic: {diagnostic}" +
                $"\n            exception: {ex.Message}");
        }

        private void UpdateCurrentDiagnostics(Project project, Document document, ImmutableArray<Diagnostic> diagnostics)
        {
            var documentDiagnostics = new DocumentDiagnostics(document.Id, document.FilePath, project.Id, project.Name, diagnostics);
            _currentDiagnosticResultLookup[document.Id] = documentDiagnostics;
            EmitDiagnostics(documentDiagnostics);
        }

        private void EmitDiagnostics(DocumentDiagnostics results)
        {
            _forwarder.Forward(new DiagnosticMessage
            {
                Results = new[]
                {
                    new DiagnosticResult
                    {
                        FileName = results.DocumentPath, QuickFixes = results.Diagnostics
                            .Select(x => x.ToDiagnosticLocation())
                            .ToList()
                    }
                }
            });
        }

        public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics()
        {
            var documentIds = _workspace.CurrentSolution.Projects.SelectMany(x => x.DocumentIds).ToImmutableArray();
            QueueForAnalysis(documentIds, AnalyzerWorkType.Background);
            return documentIds;
        }

        public async Task<ImmutableArray<DocumentDiagnostics>> GetAllDiagnosticsAsync()
        {
            var allDocumentsIds = _workspace.CurrentSolution.Projects.SelectMany(x => x.DocumentIds).ToImmutableArray();
            return await GetDiagnosticsByDocumentIds(allDocumentsIds, waitForDocuments: false);
        }

        public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(ImmutableArray<ProjectId> projectIds)
        {
            var documentIds = projectIds
                .SelectMany(projectId => _workspace.CurrentSolution.GetProject(projectId).Documents.Select(x => x.Id))
                .ToImmutableArray();
            QueueForAnalysis(documentIds, AnalyzerWorkType.Background);
            return documentIds;
        }

        public void Dispose()
        {
            _workspace.WorkspaceChanged -= OnWorkspaceChanged;
            _workspace.OnInitialized -= OnWorkspaceInitialized;
        }
    }
}
