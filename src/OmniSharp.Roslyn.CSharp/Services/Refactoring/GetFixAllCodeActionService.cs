using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Mef;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.GetFixAll, LanguageNames.CSharp)]
    public class GetFixAllCodeActionService : FixAllCodeActionBase, IRequestHandler<GetFixAllRequest, GetFixAllResponse>
    {
        [ImportingConstructor]
        public GetFixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers) : base(diagnosticWorker, codeFixProvider, workspace, providers)
        {
        }

        public async Task<GetFixAllResponse> Handle(GetFixAllRequest request)
        {
            var projectIdsInScope = GetProjectIdsInScope(request);

            var availableFixes = await Task.WhenAll(projectIdsInScope.Select(id => GetAvailableCodeFixes(id)));

            var distinctDiagnosticsThatCanBeFixed = availableFixes
                .SelectMany(x => x)
                .SelectMany(x => x.MatchingDiagnostics)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .Select(x => new FixAllItem(x.Id, x.GetMessage()))
                .ToArray();

            return new GetFixAllResponse(distinctDiagnosticsThatCanBeFixed);
        }

        private IEnumerable<ProjectId> GetProjectIdsInScope(GetFixAllRequest request)
        {
            var currentSolution = Workspace.CurrentSolution;

            if(request.Scope == FixAllScope.Solution)
                return currentSolution.Projects.Select(x => x.Id);

            return currentSolution
                .GetDocumentIdsWithFilePath(request.FileName)
                .Select(x => x.ProjectId);
        }
    }
}