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
                QueueForAnalysis(workspace.CurrentSolution.Projects.Select(x => x.Id).ToImmutableArray());
                _logger.LogInformation("Solution initialized -> queue all projects for code analysis.");
            });

            Task.Factory.StartNew(Worker, TaskCreationOptions.LongRunning);
        }

        public void QueueForDiagnosis(ImmutableArray<Document> documents)
        {
            QueueForAnalysis(GetProjectIdsFromDocuments(documents));
        }

        public async Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnostics(ImmutableArray<Document> documents)
        {
            var projectIds = GetProjectIdsFromDocuments(documents);

            await _workQueue.WaitForPendingWork(projectIds);

            return _results
                .Where(x => projectIds.Any(pid => pid == x.Key))
                .SelectMany(x => x.Value.diagnostics, (k, v) => ((k.Value.name, v)))
                .ToImmutableArray();
        }

        private async Task Worker()
        {
            while (true)
            {
                try
                {
                    var currentWork = TakeNextBatchOfProjectsForAnalysisIfAny();
                    await Task.WhenAll(currentWork.Select(x => Analyze(x)));
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Analyzer worker failed: {ex}");
                }
            }
        }

        private ImmutableArray<Project> TakeNextBatchOfProjectsForAnalysisIfAny()
        {
            return _workQueue.TakeWork()
                .Select(projectId => _workspace?.CurrentSolution?.GetProject(projectId))
                .Where(project => project != null) // This may occur if project removed middle of analysis from solution.
                .ToImmutableArray();
        }

        private void QueueForAnalysis(ImmutableArray<ProjectId> projects)
        {
            foreach (var projectId in projects)
            {
                _workQueue.PutWork(projectId);
            }
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

        private async Task Analyze(Project project)
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
                    .GetCompilationAsync();

                ImmutableArray<Diagnostic> results = ImmutableArray<Diagnostic>.Empty;

                if (allAnalyzers.Any())
                {
                    var workspaceAnalyzerOptions =
                        (AnalyzerOptions)_workspaceAnalyzerOptionsConstructor.Invoke(new object[] { project.AnalyzerOptions, project.Solution.Options, project.Solution });

                    results = await compiled
                        .WithAnalyzers(allAnalyzers, workspaceAnalyzerOptions) // This cannot be invoked with empty analyzers list.
                        .GetAllDiagnosticsAsync();
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
                _workQueue.AckWorkAsDone(project.Id);
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

        private static ImmutableArray<ProjectId> GetProjectIdsFromDocuments(ImmutableArray<Document> documents)
        {
            return documents
                .Select(x => x.Project?.Id)
                .Where(x => x != null)
                .ToImmutableArray();
        }
    }
}
