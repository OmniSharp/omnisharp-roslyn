using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.Format;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Workers.Formatting;

namespace OmniSharp.Roslyn.CSharp.Services.Formatting
{
    [OmniSharpHandler(OmniSharpEndpoints.FormatAfterKeystroke, LanguageNames.CSharp)]
    public class FormatAfterKeystrokeService : IRequestHandler<FormatAfterKeystrokeRequest, FormatRangeResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly OmniSharpOptions _omnisharpOptions;

        [ImportingConstructor]
        public FormatAfterKeystrokeService(OmniSharpWorkspace workspace, OmniSharpOptions omnisharpOptions)
        {
            _workspace = workspace;
            _omnisharpOptions = omnisharpOptions;
        }

        public async Task<FormatRangeResponse> Handle(FormatAfterKeystrokeRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var text = await document.GetTextAsync();
            int position = text.GetTextPosition(request);
            var changes = await FormattingWorker.GetFormattingChangesAfterKeystroke(document, position, request.Char, _omnisharpOptions);

            return new FormatRangeResponse()
            {
                Changes = changes
            };
        }
    }
}
