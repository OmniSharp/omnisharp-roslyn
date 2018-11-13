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
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [Shared]
    [Export(typeof(CSharpDiagnosticService))]
    public class CSharpDiagnosticService
    {
        private readonly ILogger<CSharpDiagnosticService> _logger;
        private readonly ConcurrentDictionary<ProjectId, (DateTime modified, ProjectId projectId, CancellationTokenSource workReadySource)> _workQueue =
            new ConcurrentDictionary<ProjectId, (DateTime modified, ProjectId projectId, CancellationTokenSource workReadySource)>();
        private readonly ConcurrentDictionary<ProjectId, (string name, ImmutableArray<Diagnostic> diagnostics)> _results =
            new ConcurrentDictionary<ProjectId, (string name, ImmutableArray<Diagnostic> diagnostics)>();
        private readonly ImmutableArray<ICodeActionProvider> _providers;
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmniSharpWorkspace _workspace;
        private readonly RulesetsForProjects _rulesetsForProjects;

        // This is workaround.
        // Currently roslyn doesn't expose official way to use IDE analyzers during analysis.
        // This options gives certain IDE analysis access for services that are not yet publicly available.
        private readonly ConstructorInfo _workspaceAnalyzerOptionsConstructor;

        private CancellationTokenSource _initializationQueueDoneSource = new CancellationTokenSource();
        private readonly int _throttlingMs = 300;

        [ImportingConstructor]
        public CSharpDiagnosticService(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            DiagnosticEventForwarder forwarder,
            RulesetsForProjects rulesetsForProjects,
            OmniSharpOptions options)
        {
            _logger = loggerFactory.CreateLogger<CSharpDiagnosticService>();
            _providers = providers.ToImmutableArray();


            _forwarder = forwarder;
            _workspace = workspace;
            _rulesetsForProjects = rulesetsForProjects;

            _workspaceAnalyzerOptionsConstructor = Assembly
                .Load("Microsoft.CodeAnalysis.Features")
                .GetType("Microsoft.CodeAnalysis.Diagnostics.WorkspaceAnalyzerOptions")
                .GetConstructor(new Type[] { typeof(AnalyzerOptions), typeof(OptionSet), typeof(Solution) })
                ?? throw new InvalidOperationException("Could not resolve 'Microsoft.CodeAnalysis.Diagnostics.WorkspaceAnalyzerOptions' for IDE analyzers.");

            if (options.RoslynExtensionsOptions.EnableAnalyzersSupport)
            {
                _workspace.WorkspaceChanged += OnWorkspaceChanged;

                Task.Run(async () =>
                {
                    while (!workspace.Initialized || workspace.CurrentSolution.Projects.Count() == 0) await Task.Delay(500);

                    QueueForAnalysis(workspace.CurrentSolution.Projects.Select(x => x.Id).ToImmutableArray());
                    _initializationQueueDoneSource.Cancel();
                    _logger.LogInformation("Solution initialized -> queue all projects for code analysis.");
                });

                Task.Factory.StartNew(() => Worker(CancellationToken.None), TaskCreationOptions.LongRunning);
            }
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

        private ImmutableArray<(Project project, CancellationTokenSource workReadySource)> GetThrottledWork()
        {
            lock (_workQueue)
            {
                var currentWork = _workQueue
                    .Where(x => x.Value.modified.AddMilliseconds(_throttlingMs) < DateTime.UtcNow)
                    .OrderByDescending(x => x.Value.modified) // If you currently edit project X you want it will be highest priority and contains always latest possible analysis.
                    .Take(2) // Limit mount of work executed by once. This is needed on large solution...
                    .ToImmutableArray();

                foreach (var workKey in currentWork.Select(x => x.Key))
                {
                    _workQueue.TryRemove(workKey, out _);
                }

                return currentWork
                    .Select(x => (project: _workspace?.CurrentSolution?.GetProject(x.Value.projectId), x.Value.workReadySource))
                    .Where(x => x.project != null) // This may occur if project removed middle of analysis.
                    .ToImmutableArray();
            }
        }

        public async Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetCurrentDiagnosticResult(ImmutableArray<ProjectId> projectIds)
        {
            await WaitForInitialStartupWorkIfAny();

            var pendingWork = WaitForPendingWorkIfNeededAndGetIt(projectIds);

            await Task.WhenAll(pendingWork);

            return _results
                .Where(x => projectIds.Any(pid => pid == x.Key))
                .SelectMany(x => x.Value.diagnostics, (k, v) => ((k.Value.name, v)))
                .ToImmutableArray();
        }

        public void QueueForAnalysis(ImmutableArray<ProjectId> projects)
        {
            foreach (var projectId in projects)
            {
                _workQueue.AddOrUpdate(projectId,
                    (modified: DateTime.UtcNow, projectId: projectId, new CancellationTokenSource()),
                    (_, oldValue) => (modified: DateTime.UtcNow, projectId: projectId, oldValue.workReadySource));
            }
        }

        private ImmutableArray<Task> WaitForPendingWorkIfNeededAndGetIt(ImmutableArray<ProjectId> projectIds)
        {
            return _workQueue
                .Where(x => projectIds.Any(pid => pid == x.Key))
                .Select(x => Task.Delay(30 * 1000, x.Value.workReadySource.Token)
                    .ContinueWith(task => LogTimeouts(task, x.Key.ToString())))
                .Concat(new[] { Task.Delay(250) }) // Workaround for issue where information about updates from workspace are not at sync with calls.
                .ToImmutableArray();
        }

        // Editors seems to fetch initial (get all) diagnostics too soon from api,
        // and when this happens initially api returns nothing. This causes nothing
        // to show until user action causes editor to re-fetch all diagnostics from api again.
        // For this reason initially api waits for results for moment. This isn't perfect
        // solution but hopefully works until event based diagnostics are published.
        private Task WaitForInitialStartupWorkIfAny()
        {
            return Task.Delay(30 * 1000, _initializationQueueDoneSource.Token)
                        .ContinueWith(task => LogTimeouts(task, nameof(_initializationQueueDoneSource)));
        }

        // This is basically asserting mechanism for hanging analysis if any. If this doesn't exist tracking
        // down why results doesn't come up (for example in situation when theres bad analyzer that takes ages to complete).
        private void LogTimeouts(Task task, string description)
        {
            if (!task.IsCanceled) _logger.LogError($"Timeout before work got ready for {description}.");
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            if (changeEvent.Kind == WorkspaceChangeKind.DocumentChanged
                || changeEvent.Kind == WorkspaceChangeKind.DocumentRemoved
                || changeEvent.Kind == WorkspaceChangeKind.DocumentAdded
                || changeEvent.Kind == WorkspaceChangeKind.ProjectAdded)
            {
                QueueForAnalysis(ImmutableArray.Create(changeEvent.ProjectId));
            }
        }

        private async Task Analyze(Project project, CancellationTokenSource workReadySource, CancellationToken token)
        {
            try
            {
                // Only basic syntax check is available if file is miscellanous like orphan .cs file.
                // Todo: Where this magic string should be moved?
                if (project.Name == "MiscellaneousFiles.csproj")
                {
                    await AnalyzeSingleMiscFilesProject(project);
                    return;
                }

                var allAnalyzers = _providers
                    .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                    .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzers(project.Language)))
                    .ToImmutableArray();

                var compiled = await project.WithCompilationOptions(
                    _rulesetsForProjects.BuildCompilationOptionsWithCurrentRules(project))
                    .GetCompilationAsync(token);

                ImmutableArray<Diagnostic> results = ImmutableArray<Diagnostic>.Empty;

                if (allAnalyzers.Any())
                {
                    var workspaceAnalyzerOptions =
                        (AnalyzerOptions)_workspaceAnalyzerOptionsConstructor.Invoke(new object[] { project.AnalyzerOptions, project.Solution.Options, project.Solution });

                    results = await compiled
                        .WithAnalyzers(allAnalyzers, workspaceAnalyzerOptions) // This cannot be invoked with empty analyzers list.
                        .GetAllDiagnosticsAsync(token);
                }
                else
                {
                    results = compiled.GetDiagnostics();
                }

                _results[project.Id] = (project.Name, results);

                EmitDiagnostics(results);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analysis of project {project.Id} ({project.Name}) failed, underlaying error: {ex}");
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

            _results[project.Id] = (project.Name, results.ToImmutableArray());
        }
    }
}
