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
            var projectsForAnalysis = !string.IsNullOrEmpty(request.FileName)
                ? new[] { _workspace.GetDocument(request.FileName)?.Project }
                : _workspace.CurrentSolution.Projects;

            var analyzerResults = await _roslynAnalyzer.GetCurrentDiagnosticResult(
                projectsForAnalysis.Where(x => x != null).Select(x => x.Id));

            var locations = analyzerResults
                .Where(x => (string.IsNullOrEmpty(request.FileName)
                    || x.diagnostic.Location.GetLineSpan().Path == request.FileName))
                .Select(x => new
                {
                    location = x.diagnostic.ToDiagnosticLocation(),
                    project = x.projectName
                });

            var groupedByProjectWhenMultipleFrameworksAreUsed = locations
                .GroupBy(x => x.location)
                .Select(x =>
                {
                    var location = x.First().location;
                    location.Projects = x.Select(a => a.project).ToList();
                    return location;
                });

            return new QuickFixResponse(
                groupedByProjectWhenMultipleFrameworksAreUsed.Where(x => x.FileName != null));
        }
    }
}
