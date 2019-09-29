using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
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
        protected async Task<ImmutableArray<DocumentWithFixProvidersAndMatchingDiagnostics>> GetDiagnosticsMappedWithFixAllProviders(FixAllScope scope, string fileName)
        {
            ImmutableArray<DocumentDiagnostics> allDiagnostics = await GetCorrectDiagnosticsInScope(scope, fileName);

            var mappedProvidersWithDiagnostics = allDiagnostics
                .SelectMany(diagnosticsInDocument =>
                    DocumentWithFixProvidersAndMatchingDiagnostics.CreateWithMatchingProviders(_codeFixProvider.GetAllCodeFixesForProject(diagnosticsInDocument.ProjectId), diagnosticsInDocument));

            return mappedProvidersWithDiagnostics.ToImmutableArray();
        }

        private async Task<ImmutableArray<DocumentDiagnostics>> GetCorrectDiagnosticsInScope(FixAllScope scope, string fileName)
        {
            switch (scope)
            {
                case FixAllScope.Solution:
                    var documentsInSolution = Workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).Select(x => x.FilePath).ToImmutableArray();
                    return await _diagnosticWorker.GetDiagnostics(documentsInSolution);
                case FixAllScope.Project:
                    var documentsInProject = Workspace.GetDocument(fileName).Project.Documents.Select(x => x.FilePath).ToImmutableArray();
                    return await _diagnosticWorker.GetDiagnostics(documentsInProject);
                case FixAllScope.Document:
                    return await _diagnosticWorker.GetDiagnostics(ImmutableArray.Create(fileName));
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
