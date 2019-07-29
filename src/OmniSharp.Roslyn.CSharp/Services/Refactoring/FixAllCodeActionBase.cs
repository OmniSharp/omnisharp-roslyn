using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    public abstract class FixAllCodeActionBase
    {
        private readonly ICsDiagnosticWorker _diagnosticWorker;
        private readonly CachingCodeFixProviderForProjects _codeFixProvider;
        protected OmniSharpWorkspace Workspace;
        private readonly IEnumerable<ICodeActionProvider> _providers;

        public FixAllCodeActionBase(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace, IEnumerable<ICodeActionProvider> providers)
        {
            _diagnosticWorker = diagnosticWorker;
            _codeFixProvider = codeFixProvider;
            Workspace = workspace;
            _providers = providers;
        }

        public async Task<ImmutableArray<AvailableFixAllDiagnosticProvider>> GetAvailableCodeFixes(ProjectId projectId)
        {
            var diagnostics = await _diagnosticWorker.GetAllDiagnosticsAsync();

            return
                _providers.SelectMany(provider => provider.CodeFixProviders)
                    .Concat(_codeFixProvider.GetAllCodeFixesForProject(projectId))
                    .Select(x => AvailableFixAllDiagnosticProvider.CreateOrDefault(x, diagnostics.Select(d => d.diagnostic)))
                    .Where(x => x != default)
                    .ToImmutableArray();
        }
    }
}