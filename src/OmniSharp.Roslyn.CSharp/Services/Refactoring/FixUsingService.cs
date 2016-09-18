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
        private readonly OmnisharpWorkspace _workspace;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOmnisharpAssemblyLoader _loader;
        private readonly IEnumerable<ICodeActionProvider> _providers;

        [ImportingConstructor]
        public FixUsingService(
            OmnisharpWorkspace workspace,
            ILoggerFactory loggerFactory,
            IOmnisharpAssemblyLoader loader,
            [ImportMany] IEnumerable<ICodeActionProvider> codeActionProviders)
        {
            _workspace = workspace;
            _loggerFactory = loggerFactory;
            _loader = loader;
            _providers = codeActionProviders;
        }

        public async Task<FixUsingsResponse> Handle(FixUsingsRequest request)
        {
            var response = new FixUsingsResponse();

            var document = _workspace.GetDocument(request.FileName);
            if (document != null)
            {
                var fixUsingsResponse = await new FixUsingsWorker(_providers)
                    .FixUsingsAsync(document);

                response.AmbiguousResults = fixUsingsResponse.AmbiguousResults;

                if (request.ApplyTextChanges)
                {
                    _workspace.TryApplyChanges(fixUsingsResponse.Document.Project.Solution);
                }

                if (!request.WantsTextChanges)
                {
                    // return the new document
                    var docText = await fixUsingsResponse.Document.GetTextAsync();
                    response.Buffer = docText.ToString();
                }
                else
                {
                    // return the text changes
                    var changes = await fixUsingsResponse.Document.GetTextChangesAsync(document);
                    response.Changes = await LinePositionSpanTextChange.Convert(document, changes);
                }
            }

            return response;
        }
    }
}
