using System;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Workers.Format;

namespace OmniSharp.Roslyn.CSharp.Services.Formatting
{
    [OmniSharpHandler(OmnisharpEndpoints.FormatAfterKeystroke, LanguageNames.CSharp)]
    public class FormatAfterKeystrokeService : RequestHandler<FormatAfterKeystrokeRequest, FormatRangeResponse>
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly OptionSet _options;

        [ImportingConstructor]
        public FormatAfterKeystrokeService(OmnisharpWorkspace workspace, FormattingOptions formattingOptions)
        {
            _workspace = workspace;
            _options = OmniSharp.Roslyn.CSharp.Workers.Format.Formatting.GetOptions(_workspace, formattingOptions);
        }

        public async Task<FormatRangeResponse> Handle(FormatAfterKeystrokeRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var lines = (await document.GetSyntaxTreeAsync()).GetText().Lines;
            int position = lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
            var changes = await OmniSharp.Roslyn.CSharp.Workers.Format.Formatting.GetFormattingChangesAfterKeystroke(_workspace, _options, document, position, request.Char);

            return new FormatRangeResponse()
            {
                Changes = changes
            };
        }
    }
}
