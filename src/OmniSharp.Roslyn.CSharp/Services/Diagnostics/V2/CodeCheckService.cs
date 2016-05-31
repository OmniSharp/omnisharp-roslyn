using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models.V2;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics.V2
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.CodeCheck, LanguageNames.CSharp)]
    public class CodeCheckService : RequestHandler<CodeCheckRequest, CodeCheckResponse>
    {
        private readonly DocumentDiagnosticService _diagnostics;
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public CodeCheckService(OmnisharpWorkspace workspace, DiagnosticEventForwarder forwarder, DocumentDiagnosticService diagnostics)
        {
            _forwarder = forwarder;
            _workspace = workspace;
            _diagnostics = diagnostics;
        }

        public Task<CodeCheckResponse> Handle(CodeCheckRequest request)
        {
            if (!_forwarder.IsEnabled)
            {
                _forwarder.IsEnabled = true;
            }

            var documents = (request.FileName != null
                ? _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName)
                : _workspace.CurrentSolution.Projects.SelectMany(project => project.DocumentIds))
                .Distinct().ToArray();

            _diagnostics.QueueDiagnostics(documents);

            return Task.FromResult(new CodeCheckResponse());
        }
    }
}
