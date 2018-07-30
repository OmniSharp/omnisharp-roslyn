using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using System.Collections.Generic;
using OmniSharp.Models.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.CodeCheck, LanguageNames.CSharp)]
    public class CodeCheckService : IRequestHandler<CodeCheckRequest, QuickFixResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly RoslynAnalyzerService _roslynAnalyzer;
        private readonly ILogger<CodeCheckService> _logger;

        [ImportingConstructor]
        public CodeCheckService(OmniSharpWorkspace workspace, RoslynAnalyzerService roslynAnalyzer, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _roslynAnalyzer = roslynAnalyzer;
            _logger = loggerFactory.CreateLogger<CodeCheckService>();
        }

        public async Task<QuickFixResponse> Handle(CodeCheckRequest request)
        {
            var projects = request.FileName != null
                ? _workspace.GetDocuments(request.FileName).Select(x => x.Project)
                : _workspace.CurrentSolution.Projects;

            var analyzerResults =
                await _roslynAnalyzer.GetCurrentDiagnosticResult(projects.Select(x => x.Id));

            return new QuickFixResponse(analyzerResults
                .Where(x => (request.FileName == null || x.diagnostic.Location.GetLineSpan().Path == request.FileName))
                .Select(x =>
                {
                        var asLocation = x.diagnostic.ToDiagnosticLocation();
                        asLocation.Projects = new[] { x.name };
                        return asLocation;
                })
                .Where(x => x.FileName != null));
        }
    }
}
