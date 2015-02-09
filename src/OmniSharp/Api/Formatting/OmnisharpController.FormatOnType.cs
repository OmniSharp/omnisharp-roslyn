using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("formatOnType")]
        public async Task<FormatRangeResponse> FormatOnType([FromBody]Request request)
        {
            _workspace.EnsureBufferUpdated(request);

            var document = _workspace.GetDocument(request.FileName);

            if (document == null)
            {
                return null;
            }

            var options = _workspace.Options
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.NewLine, LanguageNames.CSharp, _options.FormattingOptions.NewLine)
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.UseTabs, LanguageNames.CSharp, _options.FormattingOptions.UseTabs)
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.TabSize, LanguageNames.CSharp, _options.FormattingOptions.TabSize);

            var tree = await document.GetSyntaxTreeAsync();
            var position = tree.GetText().Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
            var edits = await Formatting.GetFormattingChangesAfterKeystroke(_workspace, options, document, position);

            return new FormatRangeResponse()
            {
                Edits = edits
            };
        }
    }
}