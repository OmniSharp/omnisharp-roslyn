using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.FixUsings;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.FixUsings, LanguageNames.CSharp)]
    public class FixUsingService : IRequestHandler<FixUsingsRequest, FixUsingsResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IEnumerable<ICodeActionProvider> _providers;

        [ImportingConstructor]
        public FixUsingService(
            OmniSharpWorkspace workspace,
            ILoggerFactory loggerFactory,
            [ImportMany] IEnumerable<ICodeActionProvider> codeActionProviders)
        {
            _workspace = workspace;
            _loggerFactory = loggerFactory;
            _providers = codeActionProviders;
        }

        public async Task<FixUsingsResponse> Handle(FixUsingsRequest request)
        {
            var response = new FixUsingsResponse();

            var oldDocument = _workspace.GetDocument(request.FileName);
            if (oldDocument != null)
            {
                var fixUsingsResponse = await new FixUsingsWorker(_providers)
                    .FixUsingsAsync(oldDocument);

                response.AmbiguousResults = fixUsingsResponse.AmbiguousResults;

                if (request.ApplyTextChanges)
                {
                    _workspace.TryApplyChanges(fixUsingsResponse.Document.Project.Solution);
                }

                var newDocument = fixUsingsResponse.Document;

                if (!request.WantsTextChanges)
                {
                    // return the text of the new document
                    var newText = await newDocument.GetTextAsync();
                    response.Buffer = newText.ToString();
                }
                else
                {
                    // return the text changes
                    response.Changes = await TextChanges.GetAsync(newDocument, oldDocument);
                }
            }

            return response;
        }
    }
}
