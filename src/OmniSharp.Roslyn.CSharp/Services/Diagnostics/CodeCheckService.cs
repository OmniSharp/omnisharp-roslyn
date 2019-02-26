using System.Collections.Generic;
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
using OmniSharp.Models.Diagnostics;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.CodeCheck, LanguageNames.CSharp)]
    public class CodeCheckService : IRequestHandler<CodeCheckRequest, QuickFixResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly OmniSharpOptions _options;
        private readonly ICsDiagnosticWorker _diagWorker;
        private readonly ILogger<CodeCheckService> _logger;

        [ImportingConstructor]
        public CodeCheckService(
            OmniSharpWorkspace workspace,
            ILoggerFactory loggerFactory,
            OmniSharpOptions options,
            ICsDiagnosticWorker diagWorker)
        {
            _workspace = workspace;
            _options = options;
            _diagWorker = diagWorker;
            _logger = loggerFactory.CreateLogger<CodeCheckService>();
        }

        public async Task<QuickFixResponse> Handle(CodeCheckRequest request)
        {
            if (request.FileName == null)
            {
                var allDiagnostics = await _diagWorker.GetAllDiagnostics();
                return GetResponseFromDiagnostics(allDiagnostics, fileName: null);
            }

            var diagnostics = await _diagWorker.GetDiagnostics(new [] { request.FileName }.ToImmutableArray());

            return GetResponseFromDiagnostics(diagnostics, request.FileName);
        }

        private static QuickFixResponse GetResponseFromDiagnostics(ImmutableArray<(string projectName, Diagnostic diagnostic)> diagnostics, string fileName)
        {
            var diagnosticLocations = diagnostics
                .Where(x => (string.IsNullOrEmpty(fileName)
                    || x.diagnostic.Location.GetLineSpan().Path == fileName))
                .DistinctDiagnosticLocationsByProject()
                .Where(x => x.FileName != null);

            return new QuickFixResponse(diagnosticLocations);
        }
    }
}
