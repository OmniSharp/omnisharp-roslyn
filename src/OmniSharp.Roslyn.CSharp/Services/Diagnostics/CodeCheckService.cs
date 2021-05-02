using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.CodeCheck, LanguageNames.CSharp)]
    public class CodeCheckService : IRequestHandler<CodeCheckRequest, QuickFixResponse>
    {
        private readonly ICsDiagnosticWorker _diagWorker;
        private readonly ILogger<CodeCheckService> _logger;

        [ImportingConstructor]
        public CodeCheckService(
            OmniSharpWorkspace workspace,
            ILoggerFactory loggerFactory,
            OmniSharpOptions options,
            ICsDiagnosticWorker diagWorker)
        {
            _diagWorker = diagWorker;
            _logger = loggerFactory.CreateLogger<CodeCheckService>();
        }

        public async Task<QuickFixResponse> Handle(CodeCheckRequest request)
        {
            if (string.IsNullOrEmpty(request.FileName))
            {
                var allDiagnostics = await _diagWorker.GetAllDiagnosticsAsync();
                return GetResponseFromDiagnostics(allDiagnostics, fileName: null);
            }

            var diagnostics = await _diagWorker.GetDiagnostics(ImmutableArray.Create(request.FileName));

            return GetResponseFromDiagnostics(diagnostics, request.FileName);
        }

        private static QuickFixResponse GetResponseFromDiagnostics(ImmutableArray<DocumentDiagnostics> diagnostics, string fileName)
        {
            var diagnosticLocations = diagnostics
                .Where(x => string.IsNullOrEmpty(fileName)
                    || x.DocumentPath == fileName)
                .DistinctDiagnosticLocationsByProject()
                .Where(x => x.FileName != null);

            return new QuickFixResponse(diagnosticLocations);
        }
    }
}
