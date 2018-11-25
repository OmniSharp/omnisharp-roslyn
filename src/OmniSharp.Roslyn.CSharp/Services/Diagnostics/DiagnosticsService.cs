using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.Diagnostics, LanguageNames.CSharp)]
    public class DiagnosticsService : IRequestHandler<DiagnosticsRequest, DiagnosticsResponse>
    {
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ICsDiagnosticWorker _diagWorker;

        [ImportingConstructor]
        public DiagnosticsService(OmniSharpWorkspace workspace, DiagnosticEventForwarder forwarder, ICsDiagnosticWorker diagWorker)
        {
            _forwarder = forwarder;
            _workspace = workspace;
            _diagWorker = diagWorker;
        }

        public Task<DiagnosticsResponse> Handle(DiagnosticsRequest request)
        {
            if (!_forwarder.IsEnabled)
            {
                _forwarder.IsEnabled = true;
            }

            var documents = request.FileName != null
                ? _workspace.GetDocuments(request.FileName)
                : _workspace.CurrentSolution.Projects.SelectMany(project => project.Documents);

            _diagWorker.QueueForDiagnosis(documents.ToImmutableArray());

            return Task.FromResult(new DiagnosticsResponse());
        }
    }
}
