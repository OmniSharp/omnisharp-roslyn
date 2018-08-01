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
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [Shared]
    [Export(typeof(RoslynAnalyzerService))]
    public class RoslynAnalyzerService
    {
        private readonly ILogger<RoslynAnalyzerService> _logger;

        private readonly ConcurrentDictionary<ProjectId, (DateTime modified, Project project, CancellationTokenSource workReadySource)> _workQueue =
            new ConcurrentDictionary<ProjectId, (DateTime modified, Project project, CancellationTokenSource workReadySource)>();

        private readonly ConcurrentDictionary<ProjectId, (string name, IEnumerable<Diagnostic> diagnostics)> _results =
            new ConcurrentDictionary<ProjectId, (string name, IEnumerable<Diagnostic> diagnostics)>();

        private readonly IEnumerable<ICodeActionProvider> _providers;

        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmniSharpWorkspace _workspace;
        private readonly RulesetsForProjects _rulesetsForProjects;
        private bool _initializationQueueDone;
        private int _throttlingMs = 500;

        [ImportingConstructor]
        public RoslynAnalyzerService(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            DiagnosticEventForwarder forwarder,
            RulesetsForProjects rulesetsForProjects)
        {
            _logger = loggerFactory.CreateLogger<RoslynAnalyzerService>();
            _providers = providers;

            workspace.WorkspaceChanged += OnWorkspaceChanged;

            Task.Factory.StartNew(() => Worker(CancellationToken.None), TaskCreationOptions.LongRunning);

            Task.Run(() =>
            {
                while (!workspace.Initialized) Task.Delay(500);
                QueueForAnalysis(workspace.CurrentSolution.Projects.ToList());
                _initializationQueueDone = true;
                _logger.LogInformation("Solution initialized -> queue all projects for code analysis.");
            });

            _forwarder = forwarder;
            _workspace = workspace;
            _rulesetsForProjects = rulesetsForProjects;
        }

        private async Task Worker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var currentWork = GetThrottledWork();
                    await Task.WhenAll(currentWork.Select(x => Analyze(x.project, x.workReadySource, token)).ToList());
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

                currentWork.Select(x => x.Key).ToList().ForEach(key => _workQueue.TryRemove(key, out _));

                return currentWork.Select(x => (x.Value.project, x.Value.workReadySource));
            }
        }

        public async Task<IEnumerable<(string projectName, Diagnostic diagnostic)>> GetCurrentDiagnosticResult(IEnumerable<ProjectId> projectIds)
        {
            while(!_initializationQueueDone) await Task.Delay(100);

            // Since results are queried, it's better to return something even if that means litle wait. (this is hack...)
            if (_workQueue.Count(x => projectIds.Any(pid => pid == x.Key)) == 0)
                await Task.Delay(250);

            var pendingWork = _workQueue
                .Where(x => projectIds.Any(pid => pid == x.Key))
                .Select(x => Task.Delay(10 * 1000, x.Value.workReadySource.Token)
                    .ContinueWith(task => LogTimeouts(task, x.Key)));

            await Task.WhenAll(pendingWork);

            return _results
                .Where(x => projectIds.Any(pid => pid == x.Key))
                .SelectMany(x => x.Value.diagnostics, (k, v) => ((k.Value.name, v)));
        }

        private void LogTimeouts(Task task, ProjectId projectId)
        {
            if(!task.IsCanceled) _logger.LogError($"Timeout before work got ready for project {projectId}.");
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

        private void QueueForAnalysis(IEnumerable<Project> projects)
        {
            projects.ToList()
                .ForEach(project => _workQueue.AddOrUpdate(project.Id,
                    (modified: DateTime.UtcNow, project: project, new CancellationTokenSource()),
                    (_, oldValue) => (modified: DateTime.UtcNow, project: project, oldValue.workReadySource)));
        }

        private async Task Analyze(Project project, CancellationTokenSource workReadySource, CancellationToken token)
        {
            try
            {
                var allAnalyzers = this._providers
                    .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                    .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzersForAllLanguages()));

                var compiled = await project.WithCompilationOptions(
                    _rulesetsForProjects.BuildCompilationOptionsWithCurrentRules(project))
                    .GetCompilationAsync(token);

                var results = await compiled
                    .WithAnalyzers(allAnalyzers.ToImmutableArray())
                    .GetAllDiagnosticsAsync(token);

                _results[project.Id] = (project.Name, results);
            }
            finally
            {
                workReadySource.Cancel();
            }
        }
    }
}
