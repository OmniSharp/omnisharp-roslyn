using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Analyzers;
using Microsoft.Extensions.Logging;
using OmniSharp.FileSystem;
using OmniSharp.Helpers;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.Events;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Helpers;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    public class CSharpDiagnosticWorkerWithAnalyzers : CSharpDiagnosticWorkerBase, IDisposable
    {
        private readonly AnalyzerWorkQueue _workQueue;
        private readonly SemaphoreSlim _throttler;
        private readonly ILogger<CSharpDiagnosticWorkerWithAnalyzers> _logger;

        private readonly ConcurrentDictionary<DocumentId, DocumentDiagnostics> _currentDiagnosticResultLookup = new();
        private readonly ImmutableArray<ICodeActionProvider> _providers;
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmniSharpOptions _options;
        private readonly OmniSharpWorkspace _workspace;
        private const int WorkerWait = 250;

        public CSharpDiagnosticWorkerWithAnalyzers(
                OmniSharpWorkspace workspace,
                [ImportMany] IEnumerable<ICodeActionProvider> providers,
                ILoggerFactory loggerFactory,
                DiagnosticEventForwarder forwarder,
                OmniSharpOptions options,
                FileSystemHelper fileSystemHelper,
                bool enableAnalyzers = true)
            : base(workspace, fileSystemHelper)
        {
            _logger = loggerFactory.CreateLogger<CSharpDiagnosticWorkerWithAnalyzers>();
            _providers = providers.ToImmutableArray();
            _workQueue = new AnalyzerWorkQueue(loggerFactory, timeoutForPendingWorkMs: options.RoslynExtensionsOptions.DocumentAnalysisTimeoutMs * 3);
            _throttler = new SemaphoreSlim(options.RoslynExtensionsOptions.DiagnosticWorkersThreadCount);

            _forwarder = forwarder;
            _options = options;
            _workspace = workspace;
            AnalyzersEnabled = enableAnalyzers;

            _workspace.WorkspaceChanged += OnWorkspaceChanged;
            _workspace.OnInitialized += OnWorkspaceInitialized;

            Task.Factory.StartNew(() => Worker(AnalyzerWorkType.Foreground), TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => Worker(AnalyzerWorkType.Background), TaskCreationOptions.LongRunning);

            OnWorkspaceInitialized(_workspace.Initialized);
        }

        public override bool AnalyzersEnabled { get; }

        public void OnWorkspaceInitialized(bool isInitialized)
        {
            if (isInitialized)
            {
                var documentIds = QueueDocumentsForDiagnostics();
                _logger.LogInformation($"Solution initialized -> queue all documents for code analysis. Initial document count: {documentIds.Length}.");
            }
        }

        public override async Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<string> documentPaths)
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

                    if (documents.IsEmpty)
                    {
                        _workQueue.WorkComplete(workType);

                        await Task.Delay(WorkerWait);

                        continue;
                    }

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

                    await Task.Delay(WorkerWait);
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

        private void QueueForAnalysis(ImmutableArray<DocumentId> documentIds, AnalyzerWorkType workType) =>
            _workQueue.PutWork(documentIds, workType);

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            switch (changeEvent.Kind)
            {
                case WorkspaceChangeKind.DocumentChanged:
                case WorkspaceChangeKind.DocumentAdded:
                case WorkspaceChangeKind.DocumentReloaded:
                case WorkspaceChangeKind.DocumentInfoChanged:
                    QueueDocumentsForDiagnostics(new DocumentId[] { changeEvent.DocumentId });
                    break;
                case WorkspaceChangeKind.DocumentRemoved:
                    if (!_currentDiagnosticResultLookup.TryRemove(changeEvent.DocumentId, out _))
                    {
                        _logger.LogDebug($"Tried to remove non existent document from analysis, document: {changeEvent.DocumentId}");
                    }
                    break;
                case WorkspaceChangeKind.AnalyzerConfigDocumentChanged:
                    _logger.LogDebug($"Analyzer config document {changeEvent.DocumentId} changed, which triggered re-analysis of project {changeEvent.ProjectId}.");
                    QueueDocumentsForDiagnostics(_workspace.CurrentSolution.GetProject(changeEvent.ProjectId));
                    break;
                case WorkspaceChangeKind.ProjectAdded:
                case WorkspaceChangeKind.ProjectChanged:
                case WorkspaceChangeKind.ProjectReloaded:
                    _logger.LogDebug($"Project {changeEvent.ProjectId} updated, reanalyzing its diagnostics.");
                    QueueDocumentsForDiagnostics(_workspace.CurrentSolution.GetProject(changeEvent.ProjectId));
                    break;
                case WorkspaceChangeKind.SolutionAdded:
                case WorkspaceChangeKind.SolutionChanged:
                case WorkspaceChangeKind.SolutionReloaded:
                    QueueDocumentsForDiagnostics();
                    break;
            }
        }

        private AnalyzerOptions CreateAnalyzerOptions(Project project)
            => OmniSharpWorkspaceAnalyzerOptionsFactory.Create(project.Solution, project.AnalyzerOptions);

        public override async Task<IEnumerable<Diagnostic>> AnalyzeDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            Project project = document.Project;
            var allAnalyzers = GetAnalyzersForProject(project);
            var compilation = await project.GetCompilationAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return await AnalyzeDocument(project, allAnalyzers, compilation, CreateAnalyzerOptions(document.Project), document);
        }

        public override async Task<IEnumerable<Diagnostic>> AnalyzeProjectsAsync(Project project, CancellationToken cancellationToken)
        {
            var allAnalyzers = GetAnalyzersForProject(project);
            var compilation = await project.GetCompilationAsync(cancellationToken);
            var workspaceAnalyzerOptions = CreateAnalyzerOptions(project);
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
                var workspaceAnalyzerOptions = CreateAnalyzerOptions(project);
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
                CancellationToken cancellationToken = new CancellationTokenSource(
                        _options.RoslynExtensionsOptions.DocumentAnalysisTimeoutMs)
                    .Token;

                // Analyzers cannot be called with empty analyzer list.
                bool canDoFullAnalysis = allAnalyzers.Length > 0
                    && (!_options.RoslynExtensionsOptions.AnalyzeOpenDocumentsOnly
                        || _workspace.IsDocumentOpen(document.Id));

                SemanticModel documentSemanticModel = await document.GetSemanticModelAsync(cancellationToken);
                SyntaxTree syntaxTree = documentSemanticModel.SyntaxTree;

                SyntaxTreeOptionsProvider provider = compilation.Options.SyntaxTreeOptionsProvider;
                GeneratedKind kind = provider.IsGenerated(syntaxTree, cancellationToken);
                if (kind is GeneratedKind.MarkedGenerated || syntaxTree.IsAutoGenerated(cancellationToken))
                {
                    return Enumerable.Empty<Diagnostic>().ToImmutableArray();
                }

                // Only basic syntax check is available if file is miscellanous like orphan .cs file.
                // Those projects are on hard coded virtual project
                if (project.Name == $"{Configuration.OmniSharpMiscProjectName}.csproj")
                {
                    return syntaxTree.GetDiagnostics().ToImmutableArray();
                }

                if (!canDoFullAnalysis)
                {
                    return documentSemanticModel.GetDiagnostics();
                }

                CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(allAnalyzers, new CompilationWithAnalyzersOptions(
                    workspaceAnalyzerOptions,
                    onAnalyzerException: OnAnalyzerException,
                    concurrentAnalysis: false,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: false));

                Task<ImmutableArray<Diagnostic>> syntaxDiagnosticsWithAnalyzers = compilationWithAnalyzers
                    .GetAnalyzerSyntaxDiagnosticsAsync(syntaxTree, cancellationToken);

                Task<ImmutableArray<Diagnostic>> semanticDiagnosticsWithAnalyzers = compilationWithAnalyzers
                    .GetAnalyzerSemanticDiagnosticsAsync(documentSemanticModel, filterSpan: null, cancellationToken);

                ImmutableArray<Diagnostic> documentSemanticDiagnostics = documentSemanticModel.GetDiagnostics(null, cancellationToken);

                await Task.WhenAll(syntaxDiagnosticsWithAnalyzers, semanticDiagnosticsWithAnalyzers);

                return semanticDiagnosticsWithAnalyzers.Result
                    .Concat(syntaxDiagnosticsWithAnalyzers.Result)
                    .Where(d => !d.IsSuppressed)
                    .Concat(documentSemanticDiagnostics)
                    .ToImmutableArray();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of document {document.Name} failed or cancelled by timeout: {ex.Message}, analysers: {string.Join(", ", allAnalyzers)}");
                return ImmutableArray<Diagnostic>.Empty;
            }
        }

        private ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForProject(Project project) =>
            AnalyzersEnabled
                ? Enumerable.Empty<DiagnosticAnalyzer>().ToImmutableArray()
                : _providers
                    .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                    .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzers(project.Language)))
                    .ToImmutableArray();

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

        public override async Task<ImmutableArray<DocumentDiagnostics>> GetAllDiagnosticsAsync()
        {
            var allDocumentsIds = _workspace.CurrentSolution.Projects.SelectMany(x => x.DocumentIds).ToImmutableArray();
            return await GetDiagnosticsByDocumentIds(allDocumentsIds, waitForDocuments: false);
        }

        public void Dispose()
        {
            _workspace.WorkspaceChanged -= OnWorkspaceChanged;
            _workspace.OnInitialized -= OnWorkspaceInitialized;
        }

        public override ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(IEnumerable<Document> documents)
        {
            ImmutableArray<DocumentId> documentsIds = documents.Select(x => x.Id).ToImmutableArray();
            QueueForAnalysis(documentsIds, AnalyzerWorkType.Background);
            return documentsIds;
        }
    }
}
