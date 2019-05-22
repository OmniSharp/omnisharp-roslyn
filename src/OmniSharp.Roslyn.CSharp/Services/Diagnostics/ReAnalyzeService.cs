using System.Composition;
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

        [ImportingConstructor]
        public ReAnalyzeService(ICsDiagnosticWorker diagWorker)
        {
            _diagWorker = diagWorker;
        }

        public Task<ReanalyzeResponse> Handle(ReAnalyzeRequest request)
        {
            _diagWorker.QueueAllDocumentsForDiagnostics();
            return Task.FromResult(new ReanalyzeResponse());
        }
    }
}
