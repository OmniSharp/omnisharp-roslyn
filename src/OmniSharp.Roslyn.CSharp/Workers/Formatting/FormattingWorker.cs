using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Formatting.EditorConfig;
using OmniSharp.Roslyn.Utilities;

namespace OmniSharp.Roslyn.CSharp.Workers.Formatting
{
    public static class FormattingWorker
    {
        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattingChangesAfterKeystroke(Document document, int position, char character, OmniSharpOptions omnisharpOptions, ILoggerFactory loggerFactory)
        {
            if (character == '\n')
            {
                // format previous line on new line
                var text = await document.GetTextAsync();
                var lines = text.Lines;
                var targetLine = lines[lines.GetLineFromPosition(position).LineNumber - 1];
                if (!string.IsNullOrWhiteSpace(targetLine.Text.ToString(targetLine.Span)))
                {
                    return await GetFormattingChanges(document, targetLine.Start, targetLine.End, omnisharpOptions, loggerFactory);
                }
            }
            else if (character == '}' || character == ';')
            {
                // format after ; and }
                var root = await document.GetSyntaxRootAsync();
                var node = FindFormatTarget(root, position);
                if (node != null)
                {
                    return await GetFormattingChanges(document, node.FullSpan.Start, node.FullSpan.End, omnisharpOptions, loggerFactory);
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

        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattingChanges(Document document, int start, int end, OmniSharpOptions omnisharpOptions, ILoggerFactory loggerFactory)
        {
            var optionSet = omnisharpOptions.FormattingOptions.EnableEditorConfigSupport
                ? await document.Project.Solution.Workspace.Options.WithEditorConfigOptions(document.FilePath, loggerFactory)
                : document.Project.Solution.Workspace.Options;
            var newDocument = await Formatter.FormatAsync(document, TextSpan.FromBounds(start, end), optionSet);

            return await TextChanges.GetAsync(newDocument, document);
        }

        public static async Task<string> GetFormattedText(Document document, OmniSharpOptions omnisharpOptions, ILoggerFactory loggerFactory)
        {
            var optionSet = omnisharpOptions.FormattingOptions.EnableEditorConfigSupport
                ? await document.Project.Solution.Workspace.Options.WithEditorConfigOptions(document.FilePath, loggerFactory)
                : document.Project.Solution.Workspace.Options;
            var newDocument = await Formatter.FormatAsync(document, optionSet);
            var text = await newDocument.GetTextAsync();

            return text.ToString();
        }

        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattedTextChanges(Document document, OmniSharpOptions omnisharpOptions, ILoggerFactory loggerFactory)
        {
            var optionSet = omnisharpOptions.FormattingOptions.EnableEditorConfigSupport
                ? await document.Project.Solution.Workspace.Options.WithEditorConfigOptions(document.FilePath, loggerFactory)
                : document.Project.Solution.Workspace.Options;
            var newDocument = await Formatter.FormatAsync(document, optionSet);

            return await TextChanges.GetAsync(newDocument, document);
        }
    }
}
