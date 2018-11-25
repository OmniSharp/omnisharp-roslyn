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
            var documents = request.FileName != null
                ? _workspace.GetDocuments(request.FileName)
                : _workspace.CurrentSolution.Projects.SelectMany(project => project.Documents);

            var diagnostics = await _diagWorker.GetDiagnostics(documents.ToImmutableArray());

            var locations = diagnostics
                .Where(x => (string.IsNullOrEmpty(request.FileName)
                    || x.diagnostic.Location.GetLineSpan().Path == request.FileName))
                .DistinctDiagnosticLocationsByProject()
                .Where(x => x.FileName != null);

            return new QuickFixResponse(locations);
        }
    }
}
