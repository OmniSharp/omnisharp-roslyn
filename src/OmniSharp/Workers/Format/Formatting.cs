using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public class Formatting
    {
        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattingChangesAfterKeystroke(Workspace workspace, OptionSet options, Document document, int position, char character)
        {
            var tree = await document.GetSyntaxTreeAsync();

            if (character == '\n')
            {
                // format previous line on new line
                var text = await document.GetTextAsync();
                var targetLine = text.Lines[text.Lines.GetLineFromPosition(position).LineNumber - 1];

                // *workaround* for not having 'indentation for line'
                var fakeDocument = document.WithText(text.WithChanges(new TextChange(TextSpan.FromBounds(position, position), ";")));
                return await GetFormattingChangesForRange(options, fakeDocument, targetLine.Start, targetLine.End);
            }
            else if(character == '}' || character == ';')
            {
                // format after ; and }
                var node = FindFormatTarget(tree, position);
                if (node != null)
                {
                    return await GetFormattingChangesForRange(options, document, node.FullSpan.Start, node.FullSpan.End);
                }
            }

            return Enumerable.Empty<LinePositionSpanTextChange>();
        }

        public static SyntaxNode FindFormatTarget(SyntaxTree tree, int position)
        {
            // todo@jo - refine this
            var token = tree.GetRoot().FindToken(position);
            var kind = token.Kind();

            switch (kind)
            {
                // ; -> use the statement
                case SyntaxKind.SemicolonToken:
                    return token.Parent;

                // } -> use the parent of the {}-block or
                // just the parent (XYZDeclaration etc)
                case SyntaxKind.CloseBraceToken:
                    var parent = token.Parent;
                    return parent.IsKind(SyntaxKind.Block)
                        ? parent.Parent
                        : parent;
            }

            return null;
        }

        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattingChangesForRange(OptionSet options, Document document, int start, int end)
        {
            var changedDocument = await Formatter.FormatAsync(document, TextSpan.FromBounds(start, end));
            var textChanges = await changedDocument.GetTextChangesAsync(document);

            return (await LinePositionSpanTextChange.Convert(document, textChanges)).Select(change =>
            {
                change.NewText = EnsureProperNewLine(change.NewText, options);
                return change;
            });
        }

        public static async Task<string> GetFormattedDocument(OptionSet options, Document document)
        {
            var formattedDocument = await Formatter.FormatAsync(document, options);
            var newText = (await formattedDocument.GetTextAsync()).ToString();
            newText = EnsureProperNewLine(newText, options);
            return newText;
        }

        private static string EnsureProperNewLine(string text, OptionSet options)
        {
            // workaround: https://github.com/dotnet/roslyn/issues/202
            var option = options.GetOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.NewLine, LanguageNames.CSharp);
            return text.Replace("\r\n", option);
        }
    }
}
