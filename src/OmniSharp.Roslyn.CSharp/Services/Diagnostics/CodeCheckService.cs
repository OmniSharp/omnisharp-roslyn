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

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.CodeCheck, LanguageNames.CSharp)]
    public class CodeCheckService : IRequestHandler<CodeCheckRequest, QuickFixResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly CSharpDiagnosticService _roslynAnalyzer;
        private readonly OmniSharpOptions _options;
        private readonly ILogger<CodeCheckService> _logger;

        [ImportingConstructor]
        public CodeCheckService(OmniSharpWorkspace workspace, CSharpDiagnosticService roslynAnalyzer, ILoggerFactory loggerFactory, OmniSharpOptions options)
        {
            _workspace = workspace;
            _roslynAnalyzer = roslynAnalyzer;
            _options = options;
            _logger = loggerFactory.CreateLogger<CodeCheckService>();
        }

        public async Task<QuickFixResponse> Handle(CodeCheckRequest request)
        {
            if(!_options.RoslynExtensionsOptions.EnableAnalyzersSupport)
            {
                var documents = request.FileName != null
                    ? _workspace.GetDocuments(request.FileName)
                    : _workspace.CurrentSolution.Projects.SelectMany(project => project.Documents);

                var quickFixes = await documents.FindDiagnosticLocationsAsync();
                return new QuickFixResponse(quickFixes);
            }

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
