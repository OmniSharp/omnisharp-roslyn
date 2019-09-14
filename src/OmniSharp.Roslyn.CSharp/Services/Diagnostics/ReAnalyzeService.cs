using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Abstractions.Models.V1.ReAnalyze;
using OmniSharp.Mef;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.ReAnalyze, LanguageNames.CSharp)]
    public class ReAnalyzeService : IRequestHandler<ReAnalyzeRequest, ReanalyzeResponse>
    {
        private readonly ICsDiagnosticWorker _diagWorker;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ILogger<ReAnalyzeService> _logger;

        [ImportingConstructor]
        public ReAnalyzeService(ICsDiagnosticWorker diagWorker, OmniSharpWorkspace workspace, ILoggerFactory loggerFactory)
        {
            _diagWorker = diagWorker;
            _workspace = workspace;
            _logger = loggerFactory.CreateLogger<ReAnalyzeService>();
        }

        public Task<ReanalyzeResponse> Handle(ReAnalyzeRequest request)
        {

            if(!string.IsNullOrEmpty(request.FileName))
            {
                var currentSolution = _workspace.CurrentSolution;

                var projectIds = WhenRequestIsProjectFileItselfGetFilesFromIt(request.FileName, currentSolution)
                    ?? GetProjectIdsFromDocumentFilePaths(request.FileName, currentSolution);

                _logger.LogInformation($"Queue analysis for project(s) {string.Join(", ", projectIds)}");

                _diagWorker.QueueDocumentsForDiagnostics(projectIds);
            }
            else
            {
                _logger.LogInformation($"Queue analysis for all projects.");
                _diagWorker.QueueDocumentsForDiagnostics();
            }

            return Task.FromResult(new ReanalyzeResponse());
        }

        private ImmutableArray<ProjectId>? WhenRequestIsProjectFileItselfGetFilesFromIt(string FileName, Solution currentSolution)
        {
            var projects = currentSolution.Projects.Where(x => CompareProjectPath(FileName, x)).Select(x => x.Id).ToImmutableArray();

            if(!projects.Any())
                return null;

            return projects;
        }

        private static bool CompareProjectPath(string FileName, Project x)
        {
            return String.Compare(
                x.FilePath,
                FileName,
                StringComparison.InvariantCultureIgnoreCase) == 0;
        }

        private static ImmutableArray<ProjectId> GetProjectIdsFromDocumentFilePaths(string FileName, Solution currentSolution)
        {
            return currentSolution
                .GetDocumentIdsWithFilePath(FileName)
                .Select(docId => currentSolution.GetDocument(docId).Project.Id)
                .Distinct()
                .ToImmutableArray();
        }
    }
}
