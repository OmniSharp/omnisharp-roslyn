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

        private readonly ConcurrentDictionary<ProjectId, (DateTime modified, Project project)> _workQueue = new ConcurrentDictionary<ProjectId, (DateTime modified, Project project)>();

        private readonly ConcurrentDictionary<ProjectId, (string name, IEnumerable<Diagnostic> diagnostics)> _results =
            new ConcurrentDictionary<ProjectId, (string name, IEnumerable<Diagnostic> diagnostics)>();

        private readonly IEnumerable<ICodeActionProvider> _providers;

        private readonly int throttlingMs = 500;

        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public RoslynAnalyzerService(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            DiagnosticEventForwarder forwarder)
        {
            _logger = loggerFactory.CreateLogger<RoslynAnalyzerService>();
            _providers = providers;

            workspace.WorkspaceChanged += OnWorkspaceChanged;

            Task.Factory.StartNew(() => Worker(CancellationToken.None), TaskCreationOptions.LongRunning);

            Task.Run(() =>
            {
                while (!workspace.Initialized) Task.Delay(500);
                QueueForAnalysis(workspace.CurrentSolution.Projects.ToList());
                _logger.LogInformation("Solution initialized -> queue all projects for code analysis.");
            });

            _forwarder = forwarder;
            _workspace = workspace;
        }

        private async Task Worker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var currentWork = GetThrottledWork();

                try
                {
                    var analyzerResults = await Task
                        .WhenAll(currentWork
                            .Select(async x => new
                            {
                                ProjectId = x.Value.Id,
                                ProjectName = x.Value.Name,
                                Result = await Analyze(x.Value, token)
                            }));

                    analyzerResults
                        .ToList()
                        .ForEach(result => _results[result.ProjectId] = (result.ProjectName, result.Result));

                    await Task.Delay(200, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Analyzer worker failed: {ex}");
                    OnErrorInitializeWithEmptyDummyIfNeeded(currentWork);
                }
            }
        }

        private void OnErrorInitializeWithEmptyDummyIfNeeded(IDictionary<ProjectId, Project> currentWork)
        {
            currentWork.ToList().ForEach(x =>
            {
                if (!_results.ContainsKey(x.Key))
                    _results[x.Key] = ($"errored {x.Key}", Enumerable.Empty<Diagnostic>());
            });
        }

        private IDictionary<ProjectId, Project> GetThrottledWork()
        {
            lock (_workQueue)
            {
                var currentWork = _workQueue
                    .Where(x => x.Value.modified.AddMilliseconds(this.throttlingMs) < DateTime.UtcNow)
                    .OrderByDescending(x => x.Value.modified) // If you currently edit project X you want it will be highest priority and contains always latest possible analysis.
                    .Take(3) // Limit mount of work executed by once. This is needed on large solution...
                    .ToList();

                currentWork.Select(x => x.Key).ToList().ForEach(key => _workQueue.TryRemove(key, out _));

                return currentWork.ToDictionary(x => x.Key, x => x.Value.project);
            }
        }

        public Task<IEnumerable<(string projectName, Diagnostic diagnostic)>> GetCurrentDiagnosticResult(IEnumerable<ProjectId> projectIds)
        {
            return Task.Run(() =>
            {
                while(!ResultsInitialized(projectIds) || PendingWork(projectIds))
                {
                    Task.Delay(100);
                }

                return _results
                    .Where(x => projectIds.Any(pid => pid == x.Key))
                    .SelectMany(x => x.Value.diagnostics, (k, v) => ((k.Value.name, v)));
            });
        }

        private bool PendingWork(IEnumerable<ProjectId> projectIds)
        {
            return projectIds.Any(x => _workQueue.ContainsKey(x));
        }

        private bool ResultsInitialized(IEnumerable<ProjectId> projectIds)
        {
            return projectIds.All(x => _results.ContainsKey(x));
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
                .ForEach(project => _workQueue.AddOrUpdate(project.Id, (modified: DateTime.UtcNow, project: project), (_, __) => (modified: DateTime.UtcNow, project: project)));
        }

        private async Task<IEnumerable<Diagnostic>> Analyze(Project project, CancellationToken token)
        {
            var allAnalyzers = this._providers
                .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzersForAllLanguages()));

            if (!allAnalyzers.Any())
                return ImmutableArray<Diagnostic>.Empty;

            var compiled = await project.GetCompilationAsync(token);

            return await compiled
                .WithAnalyzers(allAnalyzers.ToImmutableArray())
                .GetAllDiagnosticsAsync(token);
        }
    }
}
