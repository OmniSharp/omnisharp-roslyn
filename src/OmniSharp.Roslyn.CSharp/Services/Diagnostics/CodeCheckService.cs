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

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.CodeCheck, LanguageNames.CSharp)]
    public class CodeCheckService : IRequestHandler<CodeCheckRequest, QuickFixResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly RoslynAnalyzerService _roslynAnalyzer;

        [ImportingConstructor]
        public CodeCheckService(OmniSharpWorkspace workspace, RoslynAnalyzerService roslynAnalyzer)
        {
            _workspace = workspace;
            _roslynAnalyzer = roslynAnalyzer;
        }

        public async Task<QuickFixResponse> Handle(CodeCheckRequest request)
        {
            var projects = request.FileName != null
                ? _workspace.GetDocuments(request.FileName).Select(x => x.Project)
                : _workspace.CurrentSolution.Projects;

            var analyzerResults =
                await _roslynAnalyzer.GetCurrentDiagnosticResult(projects.Select(x => x.Id));

            return new QuickFixResponse(analyzerResults
                .SelectMany(x => x.Value, (key, diagnostic) => new { key, diagnostic }).Select(x =>
                {
                    var asLocation = x.diagnostic.ToDiagnosticLocation();
                    asLocation.Projects = new[] { x.key.Key };
                    return asLocation;
                }).Where(x => request.FileName == null || x.FileName == request.FileName)
                .Take(50));
        }

        private static DiagnosticLocation ToDiagnosticLocation(Diagnostic diagnostic, string project)
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            return new DiagnosticLocation
            {
                FileName = span.Path,
                Line = span.StartLinePosition.Line,
                Column = span.StartLinePosition.Character,
                EndLine = span.EndLinePosition.Line,
                EndColumn = span.EndLinePosition.Character,
                Text = $"{diagnostic.GetMessage()} ({diagnostic.Id})",
                LogLevel = diagnostic.Severity.ToString(),
                Id = diagnostic.Id
            };
        }
    }
}
