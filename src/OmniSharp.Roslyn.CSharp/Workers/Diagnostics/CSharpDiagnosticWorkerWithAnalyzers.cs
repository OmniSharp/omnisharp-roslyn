using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        private readonly ILogger<CSharpDiagnosticWorkerWithAnalyzers> _logger;

        private readonly ConditionalWeakTable<Document, DocumentDiagnostics> _currentDiagnosticResultLookup =
            new ConditionalWeakTable<Document, DocumentDiagnostics>();
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
                var documents = QueueDocumentsForDiagnostics();
                _logger.LogInformation($"Solution initialized -> queue all documents for code analysis. Initial document count: {documents.Length}.");
            }
        }

        public async Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<string> documentPaths)
        {
            var documents = documentPaths
                .Select(docPath => _workspace.GetDocument(docPath))
                .Where(x => x != default)
                .ToImmutableArray();
            return await GetDiagnosticsByDocument(documents, waitForDocuments: true);
        }

        public async Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<Document> documents)
        {
            return await GetDiagnosticsByDocument(documents, waitForDocuments: true);
        }

        private async Task<ImmutableArray<DocumentDiagnostics>> GetDiagnosticsByDocument(ImmutableArray<Document> documents, bool waitForDocuments)
        {
            if (documents.IsDefaultOrEmpty) return ImmutableArray<DocumentDiagnostics>.Empty;

            ImmutableArray<DocumentDiagnostics>.Builder resultsBuilder = ImmutableArray.CreateBuilder<DocumentDiagnostics>(documents.Length);
            resultsBuilder.Count = documents.Length;

            bool foundAll = true;

            for (int i = 0; i < documents.Length; i++)
            {
                if (_currentDiagnosticResultLookup.TryGetValue(documents[i], out var diagnostics))
                {
                    resultsBuilder[i] = diagnostics;
                }
                else
                {
                    _workQueue.QueueDocumentForeground(documents[i]);
                    foundAll = false;
                }
            }

            if (foundAll)
            {
                return resultsBuilder.MoveToImmutable();
            }

            await _workQueue.WaitForegroundWorkComplete();

            for (int i = 0; i < documents.Length; i++)
            {
                if (_currentDiagnosticResultLookup.TryGetValue(documents[i], out var diagnostics))
                {
                    resultsBuilder[i] = diagnostics;
                }
                else
                {
                    Debug.Fail("Should have diagnostics after waiting for work");
                    resultsBuilder[i] = new DocumentDiagnostics(documents[i], ImmutableArray<Diagnostic>.Empty);
                }
            }

            return resultsBuilder.MoveToImmutable();
        }

        private async Task Worker(AnalyzerWorkType workType)
        {
            while (true)
            {
                try
                {
                    var currentWorkGroupedByProjects = _workQueue
                        .TakeWork(workType)
                        .Select(document => (project: document.Project, document))
                        .GroupBy(x => x.project, x => x.document)
                        .ToImmutableArray();

                    foreach (var projectGroup in currentWorkGroupedByProjects)
                    {
                        var projectPath = projectGroup.Key.FilePath;

                        EventIfBackgroundWork(workType, projectPath, ProjectDiagnosticStatus.Started);

                        await AnalyzeProject(projectGroup);

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

        private void QueueForAnalysis(ImmutableArray<Document> documents, AnalyzerWorkType workType)
        {
            _workQueue.PutWork(documents, workType);
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
                    break;
                case WorkspaceChangeKind.ProjectAdded:
                case WorkspaceChangeKind.ProjectChanged:
                case WorkspaceChangeKind.ProjectReloaded:
                    _logger.LogDebug($"Project {changeEvent.ProjectId} updated, reanalyzing its diagnostics.");
                    var projectDocuments = _workspace.CurrentSolution.GetProject(changeEvent.ProjectId).Documents.ToImmutableArray();
                    QueueForAnalysis(projectDocuments, AnalyzerWorkType.Background);
                    break;
                case WorkspaceChangeKind.SolutionAdded:
                case WorkspaceChangeKind.SolutionChanged:
                case WorkspaceChangeKind.SolutionReloaded:
                    QueueDocumentsForDiagnostics();
                    break;

            }
        }

        private async Task AnalyzeProject(IGrouping<Project, Document> documentsGroupedByProject)
        {
            try
            {
                var project = documentsGroupedByProject.Key;
                ImmutableArray<DiagnosticAnalyzer> allAnalyzers = GetProjectAnalyzers(project);

                var compilation = await project.GetCompilationAsync();

                var workspaceAnalyzerOptions = GetWorkspaceAnalyzerOptions(project);

                foreach (var document in documentsGroupedByProject)
                {
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
            DocumentDiagnostics documentDiagnostics = new DocumentDiagnostics(document, diagnosticsWithAnalyzers);
            try
            {
                _currentDiagnosticResultLookup.Add(document, documentDiagnostics);
            }
            catch (ArgumentException)
            {
                // The work for this document was already done. Solutions (and by extension Documents) are immutable,
                // so this is fine to silently swallow, as we'll get the same results every time.
            }
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
                        FileName = results.Document.FilePath, QuickFixes = results.Diagnostics
                            .Select(x => x.ToDiagnosticLocation())
                            .ToList()
                    }
                }
            });
        }

        public ImmutableArray<Document> QueueDocumentsForDiagnostics()
        {
            var documents = _workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).ToImmutableArray();
            QueueForAnalysis(documents, AnalyzerWorkType.Background);
            return documents;
        }

        public async Task<ImmutableArray<DocumentDiagnostics>> GetAllDiagnosticsAsync()
        {
            var allDocuments = _workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).ToImmutableArray();
            return await GetDiagnosticsByDocument(allDocuments, waitForDocuments: false);
        }

        public ImmutableArray<Document> QueueDocumentsForDiagnostics(ImmutableArray<ProjectId> projectIds)
        {
            var documents = projectIds
                .SelectMany(projectId => _workspace.CurrentSolution.GetProject(projectId).Documents)
                .ToImmutableArray();
            QueueForAnalysis(documents, AnalyzerWorkType.Background);
            return documents;
        }

        public void Dispose()
        {
            _workspace.WorkspaceChanged -= OnWorkspaceChanged;
            _workspace.OnInitialized -= OnWorkspaceInitialized;
        }
    }
}
