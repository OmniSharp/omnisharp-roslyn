using System.Collections.Generic;
using System.Collections.Immutable;
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

        protected async Task<ImmutableArray<DocumentWithFixAll>> GetDiagnosticsMappedWithFixAllProviders()
        {
            var allDiagnostics = await _diagnosticWorker.GetAllDiagnosticsAsync();

            var mappedProvidersWithDiagnostics = allDiagnostics
                .SelectMany(diagnosticsInDocument =>
                    DocumentWithFixAll.CreateWithMatchingProviders(_codeFixProvider.GetAllCodeFixesForProject(diagnosticsInDocument.ProjectId), diagnosticsInDocument));

            return mappedProvidersWithDiagnostics.ToImmutableArray();
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