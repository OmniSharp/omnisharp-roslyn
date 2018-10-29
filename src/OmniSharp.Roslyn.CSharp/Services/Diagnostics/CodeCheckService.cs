using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.CodeCheck, LanguageNames.CSharp)]
    public class CodeCheckService : IRequestHandler<CodeCheckRequest, QuickFixResponse>
    {
        private OmniSharpWorkspace _workspace;
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        [ImportingConstructor]
        public CodeCheckService(OmniSharpWorkspace workspace, [ImportMany] IEnumerable<IProjectSystem> projectSystems)
        {
            _workspace = workspace;
            _projectSystems = projectSystems;
        }

        public async Task<QuickFixResponse> Handle(CodeCheckRequest request)
        {
            // Waiting until the document is fully formed in memory (for project systems that have this ability) 
            // helps to reduce chances of returning invalid list of errors while compilation is still in progress.
            await _projectSystems.WaitForAllProjectsToLoadForFileAsync(request.FileName);

            var documents = request.FileName != null
                ? _workspace.GetDocuments(request.FileName)
                : _workspace.CurrentSolution.Projects.SelectMany(project => project.Documents);

            var quickFixes = await documents.FindDiagnosticLocationsAsync(_workspace);
            return new QuickFixResponse(quickFixes);
        }
    }
}
