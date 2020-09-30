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
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    public class CSharpDiagnosticWorkerWithAnalyzers : ICsDiagnosticWorker, IDisposable
    {
        private readonly AnalyzerWorkQueue _workQueue;
        private readonly ILogger<CSharpDiagnosticWorkerWithAnalyzers> _logger;

        private readonly ConcurrentDictionary<DocumentId, DocumentDiagnostics> _currentDiagnosticResultLookup =
            new ConcurrentDictionary<DocumentId, DocumentDiagnostics>();
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
            var documentIds = GetDocumentFromPaths(documentPaths);

            return await GetDiagnosticsByDocument(documentIds, waitForDocuments: true);
        }

        public async Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<Document> documents, bool skipCache)
        {
            if (skipCache)
            {
                var resultsBuilder = ImmutableArray.CreateBuilder<DocumentDiagnostics>();
                foreach (var grouping in documents.GroupBy(doc => doc.Project))
                {
                    var project = grouping.Key;
                    var analyzers = GetProjectAnalyzers(project);
                    var compilation = await project.GetCompilationAsync();
                    var workspaceAnalyzerOptions = GetWorkspaceAnalyzerOptions(project);

                    foreach (var document in grouping)
                    {
                        resultsBuilder.Add(new DocumentDiagnostics(document, await AnalyzeDocument(project, analyzers, compilation, workspaceAnalyzerOptions, document)));
                    }
                }

                return resultsBuilder.ToImmutable();
            }

            var documentIds = documents.SelectAsArray(d => d.Id);

            return await GetDiagnosticsByDocument(documents, waitForDocuments: true);
        }

        private async Task<ImmutableArray<DocumentDiagnostics>> GetDiagnosticsByDocument(ImmutableArray<Document> documents, bool waitForDocuments)
        {
            if (waitForDocuments)
            {
                foreach (var documentId in documents)
                {
                    _workQueue.TryPromote(documentId);
                }

                await _workQueue.WaitForegroundWorkComplete();
            }

            return documents
                .Where(x => _currentDiagnosticResultLookup.ContainsKey(x.Id))
                .Select(x => _currentDiagnosticResultLookup[x.Id])
                .ToImmutableArray();
        }

        private ImmutableArray<Document> GetDocumentFromPaths(ImmutableArray<string> documentPaths)
        {
            return documentPaths
                .Select(docPath => _workspace.GetDocument(docPath))
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

                    var currentWorkGroupedByProjects = _workQueue
                        .TakeWork(workType)
                        .Select(document => (projectId: document.Project?.Id, document))
                        .Where(x => x.projectId != null)
                        .GroupBy(x => x.projectId, x => x.document.Id)
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

        private void QueueForAnalysis(ImmutableArray<Document> documentIds, AnalyzerWorkType workType)
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
                    QueueForAnalysis(ImmutableArray.Create(changeEvent.NewSolution.GetDocument(changeEvent.DocumentId)), AnalyzerWorkType.Foreground);
                    break;
                case WorkspaceChangeKind.DocumentRemoved:
                    if (!_currentDiagnosticResultLookup.TryRemove(changeEvent.DocumentId, out _))
                    {
                        _logger.LogDebug($"Tried to remove non existent document from analysis, document: {changeEvent.DocumentId}");
                    }
                    break;
                case WorkspaceChangeKind.ProjectAdded:
                case WorkspaceChangeKind.ProjectChanged:
                case WorkspaceChangeKind.ProjectReloaded:
                    _logger.LogDebug($"Project {changeEvent.ProjectId} updated, reanalyzing its diagnostics.");
                    var projectDocumentIds = _workspace.CurrentSolution.GetProject(changeEvent.ProjectId).Documents.ToImmutableArray();
                    QueueForAnalysis(projectDocumentIds, AnalyzerWorkType.Background);
                    break;
                case WorkspaceChangeKind.SolutionAdded:
                case WorkspaceChangeKind.SolutionChanged:
                case WorkspaceChangeKind.SolutionReloaded:
                    QueueDocumentsForDiagnostics();
                    break;

            }
        }

        private async Task AnalyzeProject(Solution solution, IGrouping<ProjectId, DocumentId> documentsGroupedByProject)
        {
            try
            {
                var project = solution.GetProject(documentsGroupedByProject.Key);
                ImmutableArray<DiagnosticAnalyzer> allAnalyzers = GetProjectAnalyzers(project);

                var compilation = await project.GetCompilationAsync();

                var workspaceAnalyzerOptions = GetWorkspaceAnalyzerOptions(project);

                foreach (var documentId in documentsGroupedByProject)
                {
                    var document = project.GetDocument(documentId);
                    await AnalyzeAndUpdateDocument(project, allAnalyzers, compilation, workspaceAnalyzerOptions, document);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of project {documentsGroupedByProject.Key} failed, underlaying error: {ex}");
            }
        }

        private AnalyzerOptions GetWorkspaceAnalyzerOptions(Project project)
        {
            return (AnalyzerOptions)_workspaceAnalyzerOptionsConstructor.Invoke(new object[] { project.AnalyzerOptions, project.Solution });
        }

        private ImmutableArray<DiagnosticAnalyzer> GetProjectAnalyzers(Project project)
        {
            return _providers
                .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzers(project.Language)))
                .ToImmutableArray();
        }

        private async Task AnalyzeAndUpdateDocument(Project project, ImmutableArray<DiagnosticAnalyzer> allAnalyzers, Compilation compilation, AnalyzerOptions workspaceAnalyzerOptions, Document document)
        {
            try
            {
                ImmutableArray<Diagnostic> diagnostics = await AnalyzeDocument(project, allAnalyzers, compilation, workspaceAnalyzerOptions, document);
                UpdateCurrentDiagnostics(document, diagnostics);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of document {document.Name} failed or cancelled by timeout: {ex.Message}, analysers: {string.Join(", ", allAnalyzers)}");
            }
        }

        private async Task<ImmutableArray<Diagnostic>> AnalyzeDocument(Project project, ImmutableArray<DiagnosticAnalyzer> allAnalyzers, Compilation compilation, AnalyzerOptions workspaceAnalyzerOptions, Document document)
        {
            // There's real possibility that bug in analyzer causes analysis hang at document.
            var perDocumentTimeout =
                new CancellationTokenSource(_options.RoslynExtensionsOptions.DocumentAnalysisTimeoutMs);

            var documentSemanticModel = await document.GetSemanticModelAsync(perDocumentTimeout.Token);

            var diagnostics = ImmutableArray<Diagnostic>.Empty;

            // Only basic syntax check is available if file is miscellanous like orphan .cs file.
            // Those projects are on hard coded virtual project
            if (project.Name == $"{Configuration.OmniSharpMiscProjectName}.csproj")
            {
                var syntaxTree = await document.GetSyntaxTreeAsync();
                diagnostics = syntaxTree.GetDiagnostics().ToImmutableArray();
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

                diagnostics = semanticDiagnosticsWithAnalyzers
                    .Concat(syntaxDiagnosticsWithAnalyzers)
                    .Concat(documentSemanticModel.GetDiagnostics())
                    .ToImmutableArray();
            }
            else
            {
                diagnostics = documentSemanticModel.GetDiagnostics().ToImmutableArray();
            }

            return diagnostics;
        }

        private void OnAnalyzerException(Exception ex, DiagnosticAnalyzer analyzer, Diagnostic diagnostic)
        {
            _logger.LogDebug($"Exception in diagnostic analyzer." +
                $"\n            analyzer: {analyzer}" +
                $"\n            diagnostic: {diagnostic}" +
                $"\n            exception: {ex.Message}");
        }

        private void UpdateCurrentDiagnostics(Document document, ImmutableArray<Diagnostic> diagnosticsWithAnalyzers)
        {
            _currentDiagnosticResultLookup[document.Id] = new DocumentDiagnostics(document, diagnosticsWithAnalyzers);
            EmitDiagnostics(_currentDiagnosticResultLookup[document.Id]);
        }

        private void EmitDiagnostics(DocumentDiagnostics results)
        {
            _forwarder.Forward(new DiagnosticMessage
            {
                Results = new[]
                {
                    new DiagnosticResult
                    {
                        FileName = results.Document.FilePath, QuickFixes = results.Diagnostics
                            .Select(x => x.ToDiagnosticLocation())
                            .ToList()
                    }
                }
            });
        }

        public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics()
        {
            var documents = _workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).ToImmutableArray();
            QueueForAnalysis(documents, AnalyzerWorkType.Background);
            return documents.SelectAsArray(d => d.Id);
        }

        public async Task<ImmutableArray<DocumentDiagnostics>> GetAllDiagnosticsAsync()
        {
            var allDocuments = _workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).ToImmutableArray();
            return await GetDiagnosticsByDocument(allDocuments, waitForDocuments: false);
        }

        public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(ImmutableArray<ProjectId> projectIds)
        {
            var documents = projectIds
                .SelectMany(projectId => _workspace.CurrentSolution.GetProject(projectId).Documents)
                .ToImmutableArray();
            QueueForAnalysis(documents, AnalyzerWorkType.Background);
            return documents.SelectAsArray(d => d.Id);
        }

        public void Dispose()
        {
            _workspace.WorkspaceChanged -= OnWorkspaceChanged;
            _workspace.OnInitialized -= OnWorkspaceInitialized;
        }
    }
}
