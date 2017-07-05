using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models.CodeFormat;
using OmniSharp.Roslyn.CSharp.Workers.Formatting;

namespace OmniSharp.Roslyn.CSharp.Services.Formatting
{
    [OmniSharpHandler(OmniSharpEndpoints.CodeFormat, LanguageNames.CSharp)]
    public class CodeFormatService : IRequestHandler<CodeFormatRequest, CodeFormatResponse>
    {
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public CodeFormatService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<CodeFormatResponse> Handle(CodeFormatRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            if (request.WantsTextChanges)
            {
                var textChanges = await FormattingWorker.GetFormattedTextChanges(document);
                return new CodeFormatResponse()
                {
                    Changes = textChanges
                };
            }

            var newText = await FormattingWorker.GetFormattedText(document);

            return new CodeFormatResponse
            {
                Buffer = newText
            };
        }
    }
}
