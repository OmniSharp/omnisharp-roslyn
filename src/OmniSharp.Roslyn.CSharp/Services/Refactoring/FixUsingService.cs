using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmnisharpEndpoints.FixUsings, LanguageNames.CSharp)]
    public class FixUsingService : RequestHandler<FixUsingsRequest, FixUsingsResponse>
    {
        private readonly IEnumerable<ICodeActionProvider> _codeActionProviders;
        private readonly IOmnisharpAssemblyLoader _loader;
        private readonly ILoggerFactory _loggerFactory;
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public FixUsingService(OmnisharpWorkspace workspace,
                               ILoggerFactory loggerFactory,
                               IOmnisharpAssemblyLoader loader,
                               [ImportMany] IEnumerable<ICodeActionProvider> codeActionProviders)
        {
            _workspace = workspace;
            _loggerFactory = loggerFactory;
            _loader = loader;
            _codeActionProviders = codeActionProviders;
        }

        public async Task<FixUsingsResponse> Handle(FixUsingsRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var response = new FixUsingsResponse();

            if (document != null)
            {
                response = await new FixUsingsWorker(_loggerFactory, _loader).FixUsings(_workspace, _codeActionProviders, document);

                if (!request.WantsTextChanges)
                {
                    // return the new document
                    var docText = await _workspace.CurrentSolution.GetDocument(document.Id).GetTextAsync();
                    response.Buffer = docText.ToString();
                }
                else
                {
                    // return the text changes
                    var changes = await _workspace.CurrentSolution.GetDocument(document.Id).GetTextChangesAsync(document);
                    response.Changes = await LinePositionSpanTextChange.Convert(document, changes);
                }
            }

            return response;
        }
    }
}
