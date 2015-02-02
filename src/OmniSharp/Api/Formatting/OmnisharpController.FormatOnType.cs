using System.Collections.Generic;
using System.Linq;
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
            
            var edits = new List<TextEdit>();
            var ret = new ObjectResult(new FormatRangeResponse() { Edits = edits });

            var options = _workspace.Options
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.NewLine, LanguageNames.CSharp, _options.FormattingOptions.NewLine)
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.UseTabs, LanguageNames.CSharp, _options.FormattingOptions.UseTabs)
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.TabSize, LanguageNames.CSharp, _options.FormattingOptions.TabSize);

            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return ret;
            }

            var tree = await document.GetSyntaxTreeAsync();
            var target = FindFormatTarget(tree, new LinePosition(request.Line - 1, request.Column - 1));
            if (target == null)
            {
                return ret;
            }
            
            // Instead of formatting the target node, we annotate the target node and format the
            // whole compilation unit and -using an annotation- find the formatted node in the
            // new syntax tree. That way we get the proper indentation for free.
            var annotation = new SyntaxAnnotation("formatOnTypeHelper");
            var newRoot = tree.GetRoot().ReplaceNode(target, target.WithAdditionalAnnotations(annotation));
            var formatted = Formatter.Format(newRoot, _workspace, options);
            
            var node = formatted.GetAnnotatedNodes(annotation).FirstOrDefault();
            if(node == null) 
            {
                // todo@jo - use an assert?
                return ret;
            }
            
            var linePositionSpan = tree.GetText().Lines.GetLinePositionSpan(target.FullSpan);
            var newText = node.ToFullString();
            // workaround: https://roslyn.codeplex.com/workitem/484
            newText = newText.Replace("\r\n", _options.FormattingOptions.NewLine); 
            
            edits.Add(new TextEdit(
                newText,
                linePositionSpan.Start, 
                linePositionSpan.End));

            return ret;
        }

        private SyntaxNode FindFormatTarget(SyntaxTree tree, LinePosition linePosition)
        {
            // todo@jo - refine this
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