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

        private readonly ConcurrentDictionary<string, (DateTime modified, Project project)> _workQueue = new ConcurrentDictionary<string, (DateTime modified, Project project)>();

        private readonly ConcurrentDictionary<string, IEnumerable<Diagnostic>> _results =
            new ConcurrentDictionary<string, IEnumerable<Diagnostic>>();

        private readonly IEnumerable<ICodeActionProvider> providers;

        private readonly int throttlingMs = 500;
        private readonly DiagnosticEventForwarder _forwarder;

        [ImportingConstructor]
        public RoslynAnalyzerService(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            DiagnosticEventForwarder forwarder)
        {
            _logger = loggerFactory.CreateLogger<RoslynAnalyzerService>();
            this.providers = providers;

            workspace.WorkspaceChanged += OnWorkspaceChanged;

            Task.Factory.StartNew(() => Worker(CancellationToken.None), TaskCreationOptions.LongRunning);
            Task.Run(() =>
            {
                while (!workspace.Initialized) Task.Delay(500);
                QueueForAnalysis(workspace.CurrentSolution.Projects);
                _logger.LogInformation("Solution updated, requed all projects for code analysis.");
            });
            _forwarder = forwarder;
        }

        private async Task Worker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var currentWork = GetThrottledWork();

                var analyzerResults = await Task
                    .WhenAll(currentWork
                        .Select(async x => new
                        {
                            Project = x.Key,
                            Result = await Analyze(x.Value, token)
                        }));

                analyzerResults
                    .ToList()
                    .ForEach(result => _results[result.Project] = result.Result);

                await Task.Delay(200, token);
            }
        }

        private IDictionary<string, Project> GetThrottledWork()
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

        public IDictionary<string, IEnumerable<Diagnostic>> GetCurrentDiagnosticResult() => _results.ToDictionary(x => x.Key, x => x.Value);

        public Task<Dictionary<string, IEnumerable<Diagnostic>>> GetCurrentDiagnosticResult(IEnumerable<ProjectId> projectIds)
        {
            return Task.Run(() =>
            {
                while(projectIds.Any(projectId => _workQueue.ContainsKey(projectId.ToString()))) Task.Delay(100);
                return _results.ToDictionary(x => x.Key, x => x.Value);
            });
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            if (changeEvent.Kind == WorkspaceChangeKind.DocumentChanged
                || changeEvent.Kind == WorkspaceChangeKind.ProjectChanged
                || changeEvent.Kind == WorkspaceChangeKind.ProjectAdded
                || changeEvent.Kind == WorkspaceChangeKind.ProjectReloaded)
            {
                var project = changeEvent.NewSolution.GetProject(changeEvent.ProjectId);
                ClearCurrentlyEditedFileFromAnalysisIfApplicaple(changeEvent, project);
                QueueForAnalysis(new[] { project });
            }
        }

        // This isn't perfect, but if you make change that add lines etc. for litle moment analyzers only knowns analysis from original file
        // which can cause warnings in incorrect locations if editor fetches them at that point. For this reason during analysis don't return
        // any information about that file before new result is available.
        private void ClearCurrentlyEditedFileFromAnalysisIfApplicaple(WorkspaceChangeEventArgs changeEvent, Project project)
        {
            if (changeEvent.Kind == WorkspaceChangeKind.DocumentChanged && _results.ContainsKey(project.Id.ToString()))
            {
                var updatedFilePath = changeEvent.NewSolution.GetDocument(changeEvent.DocumentId).FilePath;
                var filteredResults = _results[project.Id.ToString()].Where(x => x.Location.GetMappedLineSpan().Path != updatedFilePath);
                _results.AddOrUpdate(project.Id.ToString(), filteredResults, (_, __) => filteredResults);
            }
        }

        private void QueueForAnalysis(IEnumerable<Project> projects)
        {
            projects.ToList()
                .ForEach(project => _workQueue.AddOrUpdate(project.Id.ToString(), (modified: DateTime.UtcNow, project: project), (_, __) => (modified: DateTime.UtcNow, project: project)));
        }

        private async Task<IEnumerable<Diagnostic>> Analyze(Project project, CancellationToken token)
        {
            var allAnalyzers = this.providers
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
