using System;
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

        // Mapping means: each mapped item has one document that has one code fix provider and it's corresponding diagnostics.
        // If same document has multiple codefixers (diagnostics with different fixers) will them be mapped as separate items.
        protected async Task<ImmutableArray<DocumentWithFixProvidersAndMatchingDiagnostics>> GetDiagnosticsMappedWithFixAllProviders()
        {
            var allDiagnostics = await _diagnosticWorker.GetAllDiagnosticsAsync();

            var mappedProvidersWithDiagnostics = allDiagnostics
                .SelectMany(diagnosticsInDocument =>
                    DocumentWithFixProvidersAndMatchingDiagnostics.CreateWithMatchingProviders(_codeFixProvider.GetAllCodeFixesForProject(diagnosticsInDocument.ProjectId), diagnosticsInDocument));

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

        protected bool IsFixOnScope(DocumentWithFixProvidersAndMatchingDiagnostics documentWithFixAll, FixAllScope scope, string contextDocumentPath)
        {
            var currentSolution = Workspace.CurrentSolution;

            switch(scope)
            {
                case FixAllScope.Solution:
                    return true;
                case FixAllScope.Project:
                    return currentSolution.GetDocumentIdsWithFilePath(contextDocumentPath).Any(x => x.ProjectId == documentWithFixAll.ProjectId);
                case FixAllScope.Document:
                    return documentWithFixAll.DocumentPath == contextDocumentPath;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}