using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Workers.Formatting
{
    public static class FormattingWorker
    {
        public static OptionSet GetOptions(OmnisharpWorkspace workspace, Options.FormattingOptions formattingOptions)
        {
            return workspace.Options
                .WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, formattingOptions.NewLine)
                .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, formattingOptions.UseTabs)
                .WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, formattingOptions.TabSize)
                .WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, formattingOptions.IndentationSize);
        }

        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattingChangesAfterKeystroke(Workspace workspace, OptionSet options, Document document, int position, char character)
        {
            if (character == '\n')
            {
                // format previous line on new line
                var text = await document.GetTextAsync();
                var lines = text.Lines;
                var targetLine = lines[lines.GetLineFromPosition(position).LineNumber - 1];
                if (!string.IsNullOrWhiteSpace(targetLine.Text.ToString(targetLine.Span)))
                {
                    return await GetFormattingChangesForRange(workspace, options, document, targetLine.Start, targetLine.End);
                }
            }
            else if (character == '}' || character == ';')
            {
                // format after ; and }
                var root = await document.GetSyntaxRootAsync();
                var node = FindFormatTarget(root, position);
                if (node != null)
                {
                    return await GetFormattingChangesForRange(workspace, options, document, node.FullSpan.Start, node.FullSpan.End);
                }
            }

            return Enumerable.Empty<LinePositionSpanTextChange>();
        }

        public static SyntaxNode FindFormatTarget(SyntaxNode root, int position)
        {
            // todo@jo - refine this
            var token = root.FindToken(position);

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
            }

            return null;
        }

        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattingChangesForRange(Workspace workspace, OptionSet options, Document document, int start, int end)
        {
            var changedDocument = await Formatter.FormatAsync(document, TextSpan.FromBounds(start, end), options);
            var textChanges = await changedDocument.GetTextChangesAsync(document);

            return await LinePositionSpanTextChange.Convert(document, textChanges);
        }

        public static async Task<string> GetFormattedDocument(Workspace workspace, OptionSet options, Document document)
        {
            var formattedDocument = await Formatter.FormatAsync(document, options);
            var text = await formattedDocument.GetTextAsync();

            return text.ToString();
        }

        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattedDocumentTextChanges(Workspace workspace, OptionSet options, Document document)
        {
            var formattedDocument = await Formatter.FormatAsync(document, options);
            var textChanges = await formattedDocument.GetTextChangesAsync(document);

            return await LinePositionSpanTextChange.Convert(document, textChanges);
        }
    }
}
