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
        private readonly ICsDiagnosticWorker _diagWorker;

        [ImportingConstructor]
        public DiagnosticsService(DiagnosticEventForwarder forwarder, ICsDiagnosticWorker diagWorker)
        {
            _forwarder = forwarder;
            _diagWorker = diagWorker;
        }

        public Task<DiagnosticsResponse> Handle(DiagnosticsRequest request)
        {
            if (!_forwarder.IsEnabled)
            {
                _forwarder.IsEnabled = true;
            }

            _diagWorker.QueueDocumentsForDiagnostics();

            return Task.FromResult(new DiagnosticsResponse());
        }
    }
}
