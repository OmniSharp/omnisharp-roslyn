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
using Microsoft.Extensions.Logging;
using OmniSharp.Helpers;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [Shared]
    [Export(typeof(CSharpDiagnosticService))]
    public class CSharpDiagnosticService
    {
        private readonly ILogger<CSharpDiagnosticService> _logger;
        private readonly ConcurrentDictionary<ProjectId, (DateTime modified, Project project, CancellationTokenSource workReadySource)> _workQueue =
            new ConcurrentDictionary<ProjectId, (DateTime modified, Project project, CancellationTokenSource workReadySource)>();
        private readonly ConcurrentDictionary<ProjectId, (string name, IEnumerable<Diagnostic> diagnostics)> _results =
            new ConcurrentDictionary<ProjectId, (string name, IEnumerable<Diagnostic> diagnostics)>();
        private readonly IEnumerable<ICodeActionProvider> _providers;
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmniSharpWorkspace _workspace;
        private readonly RulesetsForProjects _rulesetsForProjects;
        private CancellationTokenSource _initializationQueueDoneSource = new CancellationTokenSource();
        private int _throttlingMs = 500;

        [ImportingConstructor]
        public CSharpDiagnosticService(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            DiagnosticEventForwarder forwarder,
            RulesetsForProjects rulesetsForProjects)
        {
            _logger = loggerFactory.CreateLogger<CSharpDiagnosticService>();
            _providers = providers;

            workspace.WorkspaceChanged += OnWorkspaceChanged;

            _forwarder = forwarder;
            _workspace = workspace;
            _rulesetsForProjects = rulesetsForProjects;

            Task.Run(async () =>
            {
                while (!workspace.Initialized || workspace.CurrentSolution.Projects.Count() == 0) await Task.Delay(500);

                QueueForAnalysis(workspace.CurrentSolution.Projects);
                _initializationQueueDoneSource.Cancel();
                _logger.LogInformation("Solution initialized -> queue all projects for code analysis.");
            });

            Task.Factory.StartNew(() => Worker(CancellationToken.None), TaskCreationOptions.LongRunning);
        }

        private async Task Worker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var currentWork = GetThrottledWork();
                    await Task.WhenAll(currentWork.Select(x => Analyze(x.project, x.workReadySource, token)));
                    await Task.Delay(100, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Analyzer worker failed: {ex}");
                }
            }
        }

        private IEnumerable<(Project project, CancellationTokenSource workReadySource)> GetThrottledWork()
        {
            lock (_workQueue)
            {
                var currentWork = _workQueue
                    .Where(x => x.Value.modified.AddMilliseconds(_throttlingMs) < DateTime.UtcNow)
                    .OrderByDescending(x => x.Value.modified) // If you currently edit project X you want it will be highest priority and contains always latest possible analysis.
                    .Take(2) // Limit mount of work executed by once. This is needed on large solution...
                    .ToList();

                foreach (var workKey in currentWork.Select(x => x.Key))
                {
                    _workQueue.TryRemove(workKey, out _);
                }

                return currentWork.Select(x => (x.Value.project, x.Value.workReadySource));
            }
        }

        public async Task<IEnumerable<(string projectName, Diagnostic diagnostic)>> GetCurrentDiagnosticResult(IEnumerable<ProjectId> projectIds)
        {
            await WaitForInitialStartupWorkIfAny();

            var pendingWork = WaitForPendingWorkIfNeededAndGetIt(projectIds);

            await Task.WhenAll(pendingWork);

            return _results
                .Where(x => projectIds.Any(pid => pid == x.Key))
                .SelectMany(x => x.Value.diagnostics, (k, v) => ((k.Value.name, v)));
        }

        public void QueueForAnalysis(IEnumerable<Project> projects)
        {
            foreach (var project in projects)
            {
                _workQueue.AddOrUpdate(project.Id,
                    (modified: DateTime.UtcNow, project: project, new CancellationTokenSource()),
                    (_, oldValue) => (modified: DateTime.UtcNow, project: project, oldValue.workReadySource));
            }
        }

        private IEnumerable<Task> WaitForPendingWorkIfNeededAndGetIt(IEnumerable<ProjectId> projectIds)
        {
            return _workQueue
                .Where(x => projectIds.Any(pid => pid == x.Key))
                .Select(x => Task.Delay(10 * 1000, x.Value.workReadySource.Token)
                    .ContinueWith(task => LogTimeouts(task, x.Key.ToString())));
        }

        private Task WaitForInitialStartupWorkIfAny()
        {
            return Task.Delay(10 * 1000, _initializationQueueDoneSource.Token)
                                .ContinueWith(task => LogTimeouts(task, nameof(_initializationQueueDoneSource)));
        }

        private void LogTimeouts(Task task, string description)
        {
            if (!task.IsCanceled) _logger.LogError($"Timeout before work got ready for {description}.");
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            if (changeEvent.Kind == WorkspaceChangeKind.DocumentChanged
                || changeEvent.Kind == WorkspaceChangeKind.DocumentAdded
                || changeEvent.Kind == WorkspaceChangeKind.ProjectAdded)
            {
                var project = changeEvent.NewSolution.GetProject(changeEvent.ProjectId);
                QueueForAnalysis(new[] { project });
            }
        }

        private async Task Analyze(Project project, CancellationTokenSource workReadySource, CancellationToken token)
        {
            try
            {
                // Only basic syntax check is available if file is miscellanous like orphan .cs file.
                // Todo: Where this magic string should be moved?
                if (project.Name == "MiscellaneousFiles")
                {
                    await AnalyzeSingleMiscFilesProject(project);
                    return;
                }

                var allAnalyzers = _providers
                    .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                    .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzersForAllLanguages()));

                var compiled = await project.WithCompilationOptions(
                    _rulesetsForProjects.BuildCompilationOptionsWithCurrentRules(project))
                    .GetCompilationAsync(token);

                ImmutableArray<Diagnostic> results = ImmutableArray<Diagnostic>.Empty;

                if (allAnalyzers.ToImmutableArray().Any())
                {
                    results = await compiled
                        .WithAnalyzers(allAnalyzers.ToImmutableArray()) // This cannot be invoked with empty analyzers list.
                        .GetAllDiagnosticsAsync(token);
                }
                else
                {
                    results = compiled.GetDiagnostics();
                }

                _results[project.Id] = (project.Name, results);

                EmitDiagnostics(results);
            }
            finally
            {
                workReadySource.Cancel();
            }
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

        private async Task AnalyzeSingleMiscFilesProject(Project project)
        {
            var syntaxTrees = await Task.WhenAll(project.Documents
                                    .Select(async document => await document.GetSyntaxTreeAsync()));

            var results = syntaxTrees
                .Select(x => x.GetDiagnostics())
                .SelectMany(x => x);

            _results[project.Id] = (project.Name, results);
        }
    }
}
