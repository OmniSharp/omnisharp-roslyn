using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{

    public partial class OmnisharpController
    {
        [HttpPost("formatOnType")]
        public async Task<IActionResult> FormatOnType([FromBody]Request request)
        {
            _workspace.EnsureBufferUpdated(request);

            var ret = new FormatRangeResponse();

            var options = _workspace.Options
                .WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, _options.FormattingOptions.NewLine)
                .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, _options.FormattingOptions.UseTabs)
                .WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, _options.FormattingOptions.TabSize);

            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return new ObjectResult(ret);
            }

            var tree = await document.GetSyntaxTreeAsync();
            var target = FindFormatTarget(tree, new LinePosition(request.Line - 1, request.Column - 1));

            if (target == null)
            {
                return new ObjectResult(ret);
            }

            var linePositionSpan = tree.GetText().Lines.GetLinePositionSpan(target.Span);
            var formattedNode = Formatter.Format(target, _workspace, options);

            ret.Edits = new TextEdit[] 
            { 
                new TextEdit()
                {
                    NewText = formattedNode.ToString().Replace("\r\n", _options.FormattingOptions.NewLine + target.GetLeadingTrivia().ToString()),
                    StartLine = linePositionSpan.Start.Line + 1,
                    StartColumn = linePositionSpan.Start.Character + 1,
                    EndLine = linePositionSpan.End.Line + 1,
                    EndColumn = linePositionSpan.End.Character + 1
                }
            };

            return new ObjectResult(ret);
        }

        private SyntaxNode FindFormatTarget(SyntaxTree tree, LinePosition linePosition)
        {
            var position = tree.GetText().Lines.GetPosition(linePosition);
            var token = tree.GetRoot().FindToken(position);
            var kind = token.CSharpKind();

            switch (kind)
            {
                // ; -> use the statement
                case SyntaxKind.SemicolonToken:
                    return token.Parent;
                // } -> use the parent of the {}-block
                case SyntaxKind.CloseBraceToken:
                    return token.Parent.Parent;
            }

            return null;
        }
    }
}