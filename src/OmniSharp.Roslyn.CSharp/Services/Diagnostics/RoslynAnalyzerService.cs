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
using OmniSharp.Models.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [Shared]
    [Export(typeof(RoslynAnalyzerService))]
    public class RoslynAnalyzerService
    {
        private readonly ILogger<RoslynAnalyzerService> _logger;
        private ConcurrentDictionary<string, (DateTime modified, Project project)> _workQueue = new ConcurrentDictionary<string, (DateTime modified, Project project)>();
        private readonly ConcurrentDictionary<string, IEnumerable<Diagnostic>> _results =
            new ConcurrentDictionary<string, IEnumerable<Diagnostic>>();
        private readonly IEnumerable<ICodeActionProvider> providers;

        private int throttlingMs = 500;

        [ImportingConstructor]
        public RoslynAnalyzerService(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<RoslynAnalyzerService>();
            this.providers = providers;

            workspace.WorkspaceChanged += OnWorkspaceChanged;

            Task.Run(() => Worker(CancellationToken.None));
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
                    .ToList();

                currentWork.Select(x => x.Key).ToList().ForEach(key => _workQueue.TryRemove(key, out _));

                return currentWork.ToDictionary(x => x.Key, x => x.Value.project);
            }
        }

        public IEnumerable<DiagnosticLocation> GetCurrentDiagnosticResults() => _results.SelectMany(x => x.Value).Select(x => AsDiagnosticLocation(x));
        public IEnumerable<Diagnostic> GetCurrentDiagnosticResults2() => _results.SelectMany(x => x.Value);

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            if (changeEvent.Kind == WorkspaceChangeKind.DocumentChanged ||
                changeEvent.Kind == WorkspaceChangeKind.ProjectChanged ||
                changeEvent.Kind == WorkspaceChangeKind.ProjectAdded ||
                changeEvent.Kind == WorkspaceChangeKind.ProjectReloaded)
            {
                var project = changeEvent.NewSolution.GetProject(changeEvent.ProjectId);
                _results.TryRemove(project.Id.ToString(), out _);
                QueueForAnalysis(new[] { changeEvent.NewSolution.GetDocument(changeEvent.DocumentId).Project });
            }
        }

        private void QueueForAnalysis(IEnumerable<Project> projects)
        {
            projects.ToList().ForEach(project =>
            {
                _workQueue.AddOrUpdate(project.Id.ToString(), (modified: DateTime.UtcNow, project: project), (key, old) => (modified: DateTime.UtcNow, project: project));
            });
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

        private static DiagnosticLocation AsDiagnosticLocation(Diagnostic diagnostic)
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            return new DiagnosticLocation
            {
                FileName = span.Path,
                Line = span.StartLinePosition.Line,
                Column = span.StartLinePosition.Character,
                EndLine = span.EndLinePosition.Line,
                EndColumn = span.EndLinePosition.Character,
                Text = $"{diagnostic.GetMessage()} ({diagnostic.Id})",
                LogLevel = diagnostic.Severity.ToString(),
                Id = diagnostic.Id,
                Projects = new List<string> {  }
            };
        }
    }
}
