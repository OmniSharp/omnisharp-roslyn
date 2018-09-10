using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.Diagnostics, LanguageNames.CSharp)]
    public class DiagnosticsService : IRequestHandler<DiagnosticsRequest, DiagnosticsResponse>
    {
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmniSharpWorkspace _workspace;
        private readonly CSharpDiagnosticService _diagnostics;

        [ImportingConstructor]
        public DiagnosticsService(OmniSharpWorkspace workspace, DiagnosticEventForwarder forwarder, CSharpDiagnosticService diagnostics)
        {
            _forwarder = forwarder;
            _workspace = workspace;
            _diagnostics = diagnostics;
        }

        public Task<DiagnosticsResponse> Handle(DiagnosticsRequest request)
        {
            if (!_forwarder.IsEnabled)
            {
                _forwarder.IsEnabled = true;
            }

            var projectsForAnalysis = !string.IsNullOrEmpty(request.FileName)
                ? new[] { _workspace.GetDocument(request.FileName)?.Project }
                : _workspace.CurrentSolution.Projects;

            _diagnostics.QueueForAnalysis(projectsForAnalysis
                .Where(x => x != null)
                .Select(x => x.Id)
                .ToImmutableArray());

            return Task.FromResult(new DiagnosticsResponse());
        }
    }
}
