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

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.CodeCheck, LanguageNames.CSharp)]
    public class CodeCheckService : IRequestHandler<CodeCheckRequest, QuickFixResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly CSharpDiagnosticService _roslynAnalyzer;
        private readonly ILogger<CodeCheckService> _logger;

        [ImportingConstructor]
        public CodeCheckService(OmniSharpWorkspace workspace, CSharpDiagnosticService roslynAnalyzer, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _roslynAnalyzer = roslynAnalyzer;
            _logger = loggerFactory.CreateLogger<CodeCheckService>();
        }

        public async Task<QuickFixResponse> Handle(CodeCheckRequest request)
        {
            var projectsForAnalysis = !string.IsNullOrEmpty(request.FileName)
                ? new[] { _workspace.GetDocument(request.FileName)?.Project }
                : _workspace.CurrentSolution.Projects;

            var analyzerResults = await _roslynAnalyzer.GetCurrentDiagnosticResult(
                projectsForAnalysis
                    .Where(project => project != null)
                    .Select(project => project.Id)
                    .ToImmutableArray());

            var locations = analyzerResults
                .Where(x => (string.IsNullOrEmpty(request.FileName)
                    || x.diagnostic.Location.GetLineSpan().Path == request.FileName))
                .DistinctDiagnosticLocationsByProject();

            return new QuickFixResponse(
                locations.Where(x => x.FileName != null));
        }
    }
}
