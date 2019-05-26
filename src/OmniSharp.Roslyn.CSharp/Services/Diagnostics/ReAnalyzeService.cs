using System;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Abstractions.Models.V1.ReAnalyze;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.ReAnalyze, LanguageNames.CSharp)]
    public class ReAnalyzeService : IRequestHandler<ReAnalyzeRequest, ReanalyzeResponse>
    {
        private readonly ICsDiagnosticWorker _diagWorker;
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public ReAnalyzeService(ICsDiagnosticWorker diagWorker, OmniSharpWorkspace workspace)
        {
            _diagWorker = diagWorker;
            _workspace = workspace;
        }

        public Task<ReanalyzeResponse> Handle(ReAnalyzeRequest request)
        {

            if(!string.IsNullOrEmpty(request.CurrentOpenFilePathAsContext))
            {
                var currentSolution = _workspace.CurrentSolution;

                var projectIds = WhenRequestIsProjectFileItselfGetFilesFromIt(request.CurrentOpenFilePathAsContext, currentSolution)
                    ?? GetDocumentsFromProjectWhereDocumentIs(request.CurrentOpenFilePathAsContext, currentSolution);

                _diagWorker.QueueDocumentsOfProjectsForDiagnostics(projectIds);
            }
            else
            {
                _diagWorker.QueueAllDocumentsForDiagnostics();
            }
            return Task.FromResult(new ReanalyzeResponse());
        }

        private ImmutableArray<ProjectId>? WhenRequestIsProjectFileItselfGetFilesFromIt(string currentOpenFilePathAsContext, Solution currentSolution)
        {
            var projects = currentSolution.Projects.Where(x => CompareProjectPath(currentOpenFilePathAsContext, x)).Select(x => x.Id).ToImmutableArray();

            if(!projects.Any())
                return null;

            return projects;
        }

        private static bool CompareProjectPath(string currentOpenFilePathAsContext, Project x)
        {
            return String.Compare(
                x.FilePath,
                currentOpenFilePathAsContext,
                StringComparison.InvariantCultureIgnoreCase) == 0;
        }

        private static ImmutableArray<ProjectId> GetDocumentsFromProjectWhereDocumentIs(string currentOpenFilePathAsContext, Solution currentSolution)
        {
            return currentSolution
                .GetDocumentIdsWithFilePath(currentOpenFilePathAsContext)
                .Select(docId => currentSolution.GetDocument(docId).Project.Id)
                .ToImmutableArray();
        }
    }
}
