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
    public class CSharpDiagnosticWorkerWithAnalyzers: ICsDiagnosticWorker
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

            Task.Run(async () =>
            {
                while (!workspace.Initialized || workspace.CurrentSolution.Projects.Count() == 0) await Task.Delay(200);
                QueueForAnalysis(workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).ToImmutableArray());
                _logger.LogInformation("Solution initialized -> queue all projects for code analysis.");
            });

            Task.Factory.StartNew(Worker, TaskCreationOptions.LongRunning);
        }

        public void QueueForDiagnosis(ImmutableArray<Document> documents)
        {
            QueueForAnalysis(documents);
        }

        public async Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnostics(ImmutableArray<Document> documents)
        {
            await _workQueue.WaitForPendingWorkDoneEvent(documents);

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

                    await Task.WhenAll(currentWorkGroupedByProjects.Select(x => Analyze(x.Key, x.ToImmutableArray())));
                    await Task.Delay(100);
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

                foreach(var document in projectDocuments)
                {
                    // Only basic syntax check is available if file is miscellanous like orphan .cs file.
                    if (allAnalyzers.Any() && project.Name != "MiscellaneousFiles.csproj")
                    {
                        var workspaceAnalyzerOptions =
                            (AnalyzerOptions)_workspaceAnalyzerOptionsConstructor.Invoke(new object[] { project.AnalyzerOptions, project.Solution.Options, project.Solution });

                        var documentSemanticModel = await document.GetSemanticModelAsync();

                        var diagnosticsWithAnalyzers = await compiled
                            .WithAnalyzers(allAnalyzers, workspaceAnalyzerOptions)
                            .GetAnalyzerSemanticDiagnosticsAsync(documentSemanticModel, filterSpan: null, CancellationToken.None); // This cannot be invoked with empty analyzers list.

                        UpdateCurrentDiagnostics(project, document, diagnosticsWithAnalyzers.Concat(documentSemanticModel.GetDiagnostics()).ToImmutableArray());
                    }
                    else
                    {
                        UpdateCurrentDiagnostics(project, document, compiled.GetDiagnostics());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of project {project.Id} ({project.Name}) failed, underlaying error: {ex}");
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
