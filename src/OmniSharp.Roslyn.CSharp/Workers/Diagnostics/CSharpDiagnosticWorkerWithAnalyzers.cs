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
        private readonly ConcurrentDictionary<DocumentId, (string projectName, ImmutableArray<Diagnostic> diagnostics)> _results =
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
                var documents = _workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).ToImmutableArray();
                QueueForAnalysis(documents);
                _logger.LogInformation($"Solution initialized -> queue all documents for code analysis. Initial document count: {documents.Length}.");
            });
        }

        public void QueueForDiagnosis(ImmutableArray<Document> documents)
        {
            QueueForAnalysis(documents);
        }

        public async Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnostics(ImmutableArray<Document> documents)
        {
            await InitializeWithWorkspaceDocumentsIfNotYetDone();

            await _workQueue.WaitWorkReadyForDocuments(documents);

            return _results
                .Where(x => documents.Any(doc => doc.Id == x.Key))
                .SelectMany(x => x.Value.diagnostics, (k, v) => ((k.Value.projectName, v)))
                .ToImmutableArray();
        }

        private async Task Worker()
        {
            while (true)
            {
                try
                {
                    var currentWorkGroupedByProjects = _workQueue
                        .TakeWork()
                        .GroupBy(x => x.Project)
                        .ToImmutableArray();

                    foreach (var projectGroup in currentWorkGroupedByProjects)
                    {
                        await Analyze(projectGroup.Key, projectGroup.ToImmutableArray());
                    }

                    await Task.Delay(50);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Analyzer worker failed: {ex}");
                }
            }
        }

        private void QueueForAnalysis(ImmutableArray<Document> documents)
        {
            foreach (var document in documents)
            {
                _workQueue.PutWork(document);
            }
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            if (changeEvent.Kind == WorkspaceChangeKind.DocumentChanged
                || changeEvent.Kind == WorkspaceChangeKind.DocumentRemoved
                || changeEvent.Kind == WorkspaceChangeKind.DocumentAdded)
            {
                QueueForAnalysis(ImmutableArray.Create(_workspace.CurrentSolution.GetDocument(changeEvent.DocumentId)));
            }
        }

        private async Task Analyze(Project project, ImmutableArray<Document> projectDocuments)
        {
            try
            {
                var allAnalyzers = _providers
                    .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                    .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzers(project.Language)))
                    .ToImmutableArray();

                var compiled = await project.WithCompilationOptions(
                    _rulesetsForProjects.BuildCompilationOptionsWithCurrentRules(project))
                    .GetCompilationAsync();

                var workspaceAnalyzerOptions =
                    (AnalyzerOptions)_workspaceAnalyzerOptionsConstructor.Invoke(new object[] { project.AnalyzerOptions, project.Solution.Options, project.Solution });

                foreach (var document in projectDocuments)
                {
                    await AnalyzeDocument(project, allAnalyzers, compiled, workspaceAnalyzerOptions, document);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of project {project.Id} ({project.Name}) failed, underlaying error: {ex}");
            }
        }

        private async Task AnalyzeDocument(Project project, ImmutableArray<DiagnosticAnalyzer> allAnalyzers, Compilation compiled, AnalyzerOptions workspaceAnalyzerOptions, Document document)
        {
            try
            {
                // Theres real possibility that bug in analyzer causes analysis hang or end to infinite loop.
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
                else if (allAnalyzers.Any())
                {
                    // Analyzers cannot be called with empty analyzer list.
                    var diagnosticsFromAnalyzers = await compiled
                        .WithAnalyzers(allAnalyzers, workspaceAnalyzerOptions)
                        .GetAnalyzerSemanticDiagnosticsAsync(documentSemanticModel, filterSpan: null, perDocumentTimeout.Token);

                    diagnostics = diagnosticsFromAnalyzers.Concat(documentSemanticModel.GetDiagnostics()).ToImmutableArray();
                }
                else
                {
                    diagnostics = documentSemanticModel.GetDiagnostics().ToImmutableArray();
                }

                UpdateCurrentDiagnostics(project, document, diagnostics);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of document {document.Name} failed or cancelled by timeout: {ex.Message}");
                _workQueue.MarkWorkAsCompleteForDocument(document);
            }
        }

        private void UpdateCurrentDiagnostics(Project project, Document document, ImmutableArray<Diagnostic> diagnosticsWithAnalyzers)
        {
            _results[document.Id] = (project.Name, diagnosticsWithAnalyzers);
            _workQueue.MarkWorkAsCompleteForDocument(document);
            EmitDiagnostics(_results[document.Id].diagnostics);
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
    }
}
