using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.CodeCheck, LanguageNames.CSharp)]
    public class CodeCheckService : IRequestHandler<CodeCheckRequest, QuickFixResponse>
    {
        private OmniSharpWorkspace _workspace;
        private readonly RoslynAnalyzerService _roslynAnalyzer;

        [ImportingConstructor]
        public CodeCheckService(OmniSharpWorkspace workspace, RoslynAnalyzerService roslynAnalyzer)
        {
            _workspace = workspace;
            _roslynAnalyzer = roslynAnalyzer;
        }

        public async Task<QuickFixResponse> Handle(CodeCheckRequest request)
        {
            var documents = request.FileName != null
                ? _workspace.GetDocuments(request.FileName)
                : _workspace.CurrentSolution.Projects.SelectMany(project => project.Documents);

            var quickFixes = await documents.FindDiagnosticLocationsAsync();
            //_roslynAnalyzer.QueueForAnalysis(documents.Select(x => x.Project).Distinct());

            var analyzerResults =
                _roslynAnalyzer.GetCurrentDiagnosticResults().Where(x =>
                    request.FileName == null || x.FileName == request.FileName);

            return new QuickFixResponse(quickFixes.Concat(analyzerResults));
        }
    }
}
