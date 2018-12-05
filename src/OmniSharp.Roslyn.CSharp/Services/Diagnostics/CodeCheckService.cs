using System.Collections.Generic;
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

        [ImportingConstructor]
        public CodeCheckService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<QuickFixResponse> Handle(CodeCheckRequest request)
        {
            var documents = request.FileName != null
                // To properly handle the request wait until all projects are loaded.
                ? await _workspace.GetDocumentsFromFullProjectModelAsync(request.FileName)
                : _workspace.CurrentSolution.Projects.SelectMany(project => project.Documents);

            var quickFixes = await documents.FindDiagnosticLocationsAsync(_workspace);
            return new QuickFixResponse(quickFixes);
        }
    }
}
