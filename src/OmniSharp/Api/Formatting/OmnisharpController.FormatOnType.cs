using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Options;

namespace OmniSharp
{

    public partial class OmnisharpController
    {
        [HttpPost("formatOnType")]
        public async Task<IActionResult> FormatOnType([FromBody]Request request)
        {
            _workspace.EnsureBufferUpdated(request);
            
            var edits = new List<TextEdit>();
            var ret = new FormatRangeResponse() { Edits = edits };

            var options = _workspace.Options
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.NewLine, LanguageNames.CSharp, _options.FormattingOptions.NewLine)
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.UseTabs, LanguageNames.CSharp, _options.FormattingOptions.UseTabs)
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.TabSize, LanguageNames.CSharp, _options.FormattingOptions.TabSize);

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
            
            var lines = tree.GetText().Lines;
            var indent = IndentForNode(target, _options);
            LinePosition start;
            LinePosition end;
            
            // format the leading trivia
            if(target.GetLeadingTrivia().ToString() != indent)
            {   
                // todo - this will remove leading comment trivia!
                start = lines.GetLinePosition(target.GetLeadingTrivia().Span.Start);
                end = lines.GetLinePosition(target.GetLeadingTrivia().Span.End);
                edits.Add(new TextEdit(indent, start, end));
            }
            
            // format the actual node
            start = lines.GetLinePosition(target.Span.Start);
            end = lines.GetLinePosition(target.Span.End);
            var newText = Formatter.Format(target, _workspace, options).ToString();
            newText = newText.Replace("\r\n", _options.FormattingOptions.NewLine + indent);
            edits.Add(new TextEdit(newText, start, end));

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
                    var candiate = token.Parent;
                    if(candiate.CSharpKind() == SyntaxKind.EmptyStatement) {
                        return null;
                    } else {
                        return candiate;
                    }
                    
                // } -> use the parent of the {}-block
                case SyntaxKind.CloseBraceToken:
                    return token.Parent.Parent;
            }

            return null;
        }
        
        private string IndentForNode(SyntaxNode node, OmniSharpOptions options)
        {
            // start with parent
            node = node.Parent;
            
            var indent = 0;
            while(node != null)
            {
                switch(node.CSharpKind())
                {
                    case SyntaxKind.NamespaceDeclaration:
                    case SyntaxKind.ClassDeclaration:
                    case SyntaxKind.InterfaceDeclaration:
                    case SyntaxKind.StructDeclaration:
                    case SyntaxKind.EnumDeclaration:
                    case SyntaxKind.MethodDeclaration:
                    case SyntaxKind.SimpleLambdaExpression:
                    case SyntaxKind.ParenthesizedLambdaExpression:
                    case SyntaxKind.SetAccessorDeclaration:
                    case SyntaxKind.GetAccessorDeclaration:
                        indent += 1;
                        break;
                }
                
                node = node.Parent;
            }
            
            return options.FormattingOptions.UseTabs 
                ? new String('\t', indent)
                : new String(' ', indent * options.FormattingOptions.TabSize);
        }
    }
}