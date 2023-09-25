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
        private readonly AsyncAnalyzerWorkQueue _workQueue;
        private readonly ILogger<CSharpDiagnosticWorkerWithAnalyzers> _logger;

        private readonly ConcurrentDictionary<DocumentId, DocumentDiagnostics> _currentDiagnosticResultLookup = new();
        private readonly ImmutableArray<ICodeActionProvider> _providers;
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmniSharpOptions _options;
        private readonly OmniSharpWorkspace _workspace;

        private int _projectCount = 0;

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
            _workQueue = new AsyncAnalyzerWorkQueue(loggerFactory);

            _forwarder = forwarder;
            _options = options;
            _workspace = workspace;
            AnalyzersEnabled = enableAnalyzers;

            _workspace.WorkspaceChanged += OnWorkspaceChanged;
            _workspace.OnInitialized += OnWorkspaceInitialized;

            for (var i = 0; i < options.RoslynExtensionsOptions.DiagnosticWorkersThreadCount; i++)
                Task.Run(Worker);

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
            ImmutableArray<DocumentId> documentIds = (await Task.WhenAll(
                               documentPaths
                                   .Select(docPath => _workspace.GetDocumentsFromFullProjectModelAsync(docPath))))
                .SelectMany(s => s)
                .Select(x => x.Id)
                .ToImmutableArray();

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

                using var cancellationTokenSource = new CancellationTokenSource(_options.RoslynExtensionsOptions.DocumentAnalysisTimeoutMs * 3);

                await _workQueue.WaitForegroundWorkComplete(cancellationTokenSource.Token);
            }

            return documentIds
                .Select(x => _currentDiagnosticResultLookup.TryGetValue(x, out var value) ? value : null)
                .Where(x => x != null)
                .ToImmutableArray();
        }

        private async Task Worker()
        {
            while (true)
            {
                AsyncAnalyzerWorkQueue.QueueItem item = null;
                DocumentId documentId;
                CancellationToken? cancellationToken = null;
                AnalyzerWorkType workType;
                int documentCount;
                int remaining;

                try
                {
                    item = await _workQueue.TakeWorkAsync();
                    (documentId, cancellationToken, workType, documentCount, remaining) = item;

                    if (workType == AnalyzerWorkType.Background)
                    {
                        // event every percentage increase, or every 10th if there are fewer than 1000
                        var eventEvery = Math.Max(10, documentCount / 100);

                        if (documentCount == remaining + 1)
                            EventIfBackgroundWork(workType, BackgroundDiagnosticStatus.Started, _projectCount, documentCount, remaining);

                        var done = documentCount - remaining;
                        if (done % eventEvery == 0 || remaining == 0)
                            EventIfBackgroundWork(workType, BackgroundDiagnosticStatus.Progress, _projectCount, documentCount, remaining);
                    }

                    var solution = _workspace.CurrentSolution;
                    var projectId = solution.GetDocument(documentId)?.Project?.Id;

                    try
                    {
                        if (projectId != null)
                            await AnalyzeDocument(solution, projectId, documentId, cancellationToken.Value);
                    }
                    finally
                    {
                        if (remaining == 0)
                            EventIfBackgroundWork(workType, BackgroundDiagnosticStatus.Finished, _projectCount, documentCount, remaining);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken != null && cancellationToken.Value.IsCancellationRequested)
                {
                    _logger.LogInformation($"Analyzer work cancelled.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Analyzer worker failed: {ex}");
                }
                finally
                {
                    if (item != null)
                        _workQueue.WorkComplete(item);
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
            if (workType == AnalyzerWorkType.Background)
            {
                var solution = _workspace.CurrentSolution;

                _projectCount = documentIds.Select(x => solution.GetDocument(x)?.Project?.Id).Distinct().Count(x => x != null);
            }

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
                    QueueDocumentsForDiagnostics(new DocumentId[] { changeEvent.DocumentId }, AnalyzerWorkType.Foreground);
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

            return await AnalyzeDocument(project, allAnalyzers, compilation, CreateAnalyzerOptions(document.Project), document, cancellationToken);
        }

        public override async Task<IEnumerable<Diagnostic>> AnalyzeProjectsAsync(Project project, CancellationToken cancellationToken)
        {
            var documentIds = project.DocumentIds.ToImmutableArray();

            QueueForAnalysis(documentIds, AnalyzerWorkType.Foreground);

            await _workQueue.WaitForegroundWorkComplete(cancellationToken);

            return documentIds
                .Select(x => _currentDiagnosticResultLookup.TryGetValue(x, out var value) ? value : null)
                .Where(x => x != null)
                .SelectMany(x => x.Diagnostics)
                .ToImmutableArray();
        }

        private async Task AnalyzeDocument(Solution solution, ProjectId projectId, DocumentId documentId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var project = solution.GetProject(projectId);
                var allAnalyzers = GetAnalyzersForProject(project);
                var compilation = await project.GetCompilationAsync();
                var workspaceAnalyzerOptions = CreateAnalyzerOptions(project);
                var document = project.GetDocument(documentId);

                var diagnostics = await AnalyzeDocument(project, allAnalyzers, compilation, workspaceAnalyzerOptions, document, cancellationToken);

                UpdateCurrentDiagnostics(project, document, diagnostics);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of document {documentId} failed, underlying error: {ex}");
            }
        }

        private async Task<ImmutableArray<Diagnostic>> AnalyzeDocument(Project project, ImmutableArray<DiagnosticAnalyzer> allAnalyzers, Compilation compilation, AnalyzerOptions workspaceAnalyzerOptions, Document document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // There's real possibility that bug in analyzer causes analysis hang at document.
            using var perDocumentTimeout =
                new CancellationTokenSource(_options.RoslynExtensionsOptions.DocumentAnalysisTimeoutMs);
            using var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, perDocumentTimeout.Token);

            try
            {
                var documentSemanticModel = await document.GetSemanticModelAsync(combinedCancellation.Token);

                // Analyzers cannot be called with empty analyzer list.
                bool canDoFullAnalysis = allAnalyzers.Length > 0
                    && (!_options.RoslynExtensionsOptions.AnalyzeOpenDocumentsOnly
                        || _workspace.IsDocumentOpen(document.Id));

                SyntaxTree syntaxTree = documentSemanticModel.SyntaxTree;

                SyntaxTreeOptionsProvider provider = compilation.Options.SyntaxTreeOptionsProvider;
                GeneratedKind kind = provider.IsGenerated(syntaxTree, combinedCancellation.Token);
                if (kind is GeneratedKind.MarkedGenerated || syntaxTree.IsAutoGenerated(combinedCancellation.Token))
                {
                    return Enumerable.Empty<Diagnostic>().ToImmutableArray();
                }

                // Only basic syntax check is available if file is miscellanous like orphan .cs file.
                // Those projects are on hard coded virtual project
                if (project.Name == $"{Configuration.OmniSharpMiscProjectName}.csproj")
                {
                    return syntaxTree.GetDiagnostics(cancellationToken: combinedCancellation.Token).ToImmutableArray();
                }

                if (!canDoFullAnalysis)
                {
                    return documentSemanticModel.GetDiagnostics(cancellationToken: combinedCancellation.Token);
                }

                CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(allAnalyzers, new CompilationWithAnalyzersOptions(
                    workspaceAnalyzerOptions,
                    onAnalyzerException: OnAnalyzerException,
                    concurrentAnalysis: false,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: false));

                Task<ImmutableArray<Diagnostic>> semanticDiagnosticsWithAnalyzers = compilationWithAnalyzers
                    .GetAnalyzerSemanticDiagnosticsAsync(documentSemanticModel, filterSpan: null, combinedCancellation.Token);

                Task<ImmutableArray<Diagnostic>> syntaxDiagnosticsWithAnalyzers = compilationWithAnalyzers
                    .GetAnalyzerSyntaxDiagnosticsAsync(syntaxTree, combinedCancellation.Token);

                ImmutableArray<Diagnostic> documentSemanticDiagnostics = documentSemanticModel.GetDiagnostics(null, combinedCancellation.Token);

                await Task.WhenAll(syntaxDiagnosticsWithAnalyzers, semanticDiagnosticsWithAnalyzers);

                return semanticDiagnosticsWithAnalyzers.Result
                    .Concat(syntaxDiagnosticsWithAnalyzers.Result)
                    .Where(d => !d.IsSuppressed)
                    .Concat(documentSemanticDiagnostics)
                    .ToImmutableArray();
            }
            catch (OperationCanceledException) when (combinedCancellation.Token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of document {document.Name} failed or cancelled by timeout: {ex.Message}, analysers: {string.Join(", ", allAnalyzers)}");
                return ImmutableArray<Diagnostic>.Empty;
            }
        }

        private ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForProject(Project project) =>
            AnalyzersEnabled
                ? _providers
                    .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                    .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzers(project.Language)))
                    .ToImmutableArray()
                : Enumerable.Empty<DiagnosticAnalyzer>().ToImmutableArray();

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
            return await GetDiagnosticsByDocumentIds(allDocumentsIds, waitForDocuments: !AnalyzersEnabled);
        }

        public void Dispose()
        {
            _workspace.WorkspaceChanged -= OnWorkspaceChanged;
            _workspace.OnInitialized -= OnWorkspaceInitialized;
        }

        public override ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(IEnumerable<Document> documents, AnalyzerWorkType workType)
        {
            ImmutableArray<DocumentId> documentsIds = documents.Select(x => x.Id).ToImmutableArray();
            QueueForAnalysis(documentsIds, AnalyzerWorkType.Background);
            return documentsIds;
        }
    }
}
