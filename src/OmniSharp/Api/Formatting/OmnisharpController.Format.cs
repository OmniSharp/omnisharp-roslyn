using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("formatAfterKeystroke")]
        public async Task<FormatRangeResponse> FormatAfterKeystroke(FormatAfterKeystrokeRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var lines = (await document.GetSyntaxTreeAsync()).GetText().Lines;
            var position = lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
            var changes = await Formatting.GetFormattingChangesAfterKeystroke(_workspace, Options, document, position, request.Char);

            return new FormatRangeResponse()
            {
                Changes = changes
            };
        }

        [HttpPost("formatRange")]
        public async Task<FormatRangeResponse> FormatRange(FormatRangeRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var lines = (await document.GetSyntaxTreeAsync()).GetText().Lines;
            var start = lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
            var end = lines.GetPosition(new LinePosition(request.EndLine - 1, request.EndColumn - 1));
            var changes = await Formatting.GetFormattingChangesForRange(_workspace, Options, document, start, end);

            return new FormatRangeResponse()
            {
                Changes = changes
            };
        }

        [HttpPost("codeformat")]
        public async Task<CodeFormatResponse> FormatDocument(Request request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var newText = await Formatting.GetFormattedDocument(_workspace, Options, document);
            return new CodeFormatResponse()
            {
                Buffer = newText
            };
        }

        private OptionSet Options
        {
            get {
                return _workspace.Options
                    .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.NewLine, LanguageNames.CSharp, _options.FormattingOptions.NewLine)
                    .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.UseTabs, LanguageNames.CSharp, _options.FormattingOptions.UseTabs)
                    .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.TabSize, LanguageNames.CSharp, _options.FormattingOptions.TabSize);
            }
        }
    }
}