using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Workers.Formatting
{
    public static class FormattingWorker
    {
        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattingChangesAfterKeystroke(Workspace workspace, Document document, int position, char character)
        {
            if (character == '\n')
            {
                // format previous line on new line
                var text = await document.GetTextAsync();
                var lines = text.Lines;
                var targetLine = lines[lines.GetLineFromPosition(position).LineNumber - 1];
                if (!string.IsNullOrWhiteSpace(targetLine.Text.ToString(targetLine.Span)))
                {
                    return await GetFormattingChangesForRange(workspace, document, targetLine.Start, targetLine.End);
                }
            }
            else if (character == '}' || character == ';')
            {
                // format after ; and }
                var root = await document.GetSyntaxRootAsync();
                var node = FindFormatTarget(root, position);
                if (node != null)
                {
                    return await GetFormattingChangesForRange(workspace, document, node.FullSpan.Start, node.FullSpan.End);
                }
            }

            return Enumerable.Empty<LinePositionSpanTextChange>();
        }

        public static SyntaxNode FindFormatTarget(SyntaxNode root, int position)
        {
            // todo@jo - refine this
            var token = root.FindToken(position);

            if (token.IsKind(SyntaxKind.EndOfFileToken))
            {
                token = token.GetPreviousToken();
            }

            switch (token.Kind())
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

                case SyntaxKind.CloseParenToken:
                    if (token.GetPreviousToken().IsKind(SyntaxKind.SemicolonToken) &&
                        token.Parent.IsKind(SyntaxKind.ForStatement))
                    {
                        return token.Parent;
                    }

                    break;
            }

            return null;
        }

        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattingChangesForRange(Workspace workspace, Document document, int start, int end)
        {
            var changedDocument = await Formatter.FormatAsync(document, TextSpan.FromBounds(start, end), workspace.Options);
            var textChanges = await changedDocument.GetTextChangesAsync(document);

            return await LinePositionSpanTextChange.Convert(document, textChanges);
        }

        public static async Task<string> GetFormattedDocument(Workspace workspace, Document document)
        {
            var formattedDocument = await Formatter.FormatAsync(document, workspace.Options);
            var text = await formattedDocument.GetTextAsync();

            return text.ToString();
        }

        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattedDocumentTextChanges(Workspace workspace, Document document)
        {
            var formattedDocument = await Formatter.FormatAsync(document, workspace.Options);
            var textChanges = await formattedDocument.GetTextChangesAsync(document);

            return await LinePositionSpanTextChange.Convert(document, textChanges);
        }
    }
}
