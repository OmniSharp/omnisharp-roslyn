using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    public abstract class FixAllCodeActionBase
    {
        private readonly ICsDiagnosticWorker _diagnosticWorker;
        private readonly CachingCodeFixProviderForProjects _codeFixProvider;
        protected OmniSharpWorkspace Workspace;

        public FixAllCodeActionBase(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace)
        {
            _diagnosticWorker = diagnosticWorker;
            _codeFixProvider = codeFixProvider;
            Workspace = workspace;
        }

        protected async Task<IEnumerable<(Diagnostics.DocumentDiagnostics diagnosticsInDocument, AvailableFixAllDiagnosticProvider provider)>> GetDiagnosticsMappedWithFixAllProviders(IEnumerable<ProjectId> projectIds)
        {
            var availableCodeFixesLookup = projectIds.ToDictionary(projectId => projectId, projectId => _codeFixProvider.GetAllCodeFixesForProject(projectId));

            var allDiagnostics = await _diagnosticWorker.GetAllDiagnosticsAsync();

            var mappedProvidersWithDiagnostics = allDiagnostics
                .SelectMany(diagnosticsInDocument =>
                    AvailableFixAllDiagnosticProvider.Create(availableCodeFixesLookup[diagnosticsInDocument.ProjectId], diagnosticsInDocument),
                    (parent, child) => (diagnosticsInDocument: parent, provider: child));

            return mappedProvidersWithDiagnostics;
        }

        protected IEnumerable<ProjectId> GetProjectIdsInScope(FixAllScope scope, string fileNameIfAny)
        {
            var currentSolution = Workspace.CurrentSolution;

            if(scope == FixAllScope.Solution)
                return currentSolution.Projects.Select(x => x.Id);

            return currentSolution
                .GetDocumentIdsWithFilePath(fileNameIfAny)
                .Select(x => x.ProjectId);
        }
    }
}