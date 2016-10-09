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
        public static OptionSet GetOptions(OmnisharpWorkspace workspace, Options.FormattingOptions formattingOptions)
        {
            return workspace.Options
                .WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, formattingOptions.NewLine)
                .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, formattingOptions.UseTabs)
                .WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, formattingOptions.TabSize)
                .WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, formattingOptions.IndentationSize)
                .WithChangedOption(CSharpFormattingOptions.SpacingAfterMethodDeclarationName, formattingOptions.SpacingAfterMethodDeclarationName)
                .WithChangedOption(CSharpFormattingOptions.SpaceWithinMethodDeclarationParenthesis, formattingOptions.SpaceWithinMethodDeclarationParenthesis)
                .WithChangedOption(CSharpFormattingOptions.SpaceBetweenEmptyMethodDeclarationParentheses, formattingOptions.SpaceBetweenEmptyMethodDeclarationParentheses)
                .WithChangedOption(CSharpFormattingOptions.SpaceAfterMethodCallName, formattingOptions.SpaceAfterMethodCallName)
                .WithChangedOption(CSharpFormattingOptions.SpaceWithinMethodCallParentheses, formattingOptions.SpaceWithinMethodCallParentheses)
                .WithChangedOption(CSharpFormattingOptions.SpaceBetweenEmptyMethodCallParentheses, formattingOptions.SpaceBetweenEmptyMethodCallParentheses)
                .WithChangedOption(CSharpFormattingOptions.SpaceAfterControlFlowStatementKeyword, formattingOptions.SpaceAfterControlFlowStatementKeyword)
                .WithChangedOption(CSharpFormattingOptions.SpaceWithinExpressionParentheses, formattingOptions.SpaceWithinExpressionParentheses)
                .WithChangedOption(CSharpFormattingOptions.SpaceWithinCastParentheses, formattingOptions.SpaceWithinCastParentheses)
                .WithChangedOption(CSharpFormattingOptions.SpaceWithinOtherParentheses, formattingOptions.SpaceWithinOtherParentheses)
                .WithChangedOption(CSharpFormattingOptions.SpaceAfterCast, formattingOptions.SpaceAfterCast)
                .WithChangedOption(CSharpFormattingOptions.SpacesIgnoreAroundVariableDeclaration, formattingOptions.SpacesIgnoreAroundVariableDeclaration)
                .WithChangedOption(CSharpFormattingOptions.SpaceBeforeOpenSquareBracket, formattingOptions.SpaceBeforeOpenSquareBracket)
                .WithChangedOption(CSharpFormattingOptions.SpaceBetweenEmptySquareBrackets, formattingOptions.SpaceBetweenEmptySquareBrackets)
                .WithChangedOption(CSharpFormattingOptions.SpaceWithinSquareBrackets, formattingOptions.SpaceWithinSquareBrackets)
                .WithChangedOption(CSharpFormattingOptions.SpaceAfterColonInBaseTypeDeclaration, formattingOptions.SpaceAfterColonInBaseTypeDeclaration)
                .WithChangedOption(CSharpFormattingOptions.SpaceAfterComma, formattingOptions.SpaceAfterComma)
                .WithChangedOption(CSharpFormattingOptions.SpaceAfterDot, formattingOptions.SpaceAfterDot)
                .WithChangedOption(CSharpFormattingOptions.SpaceAfterSemicolonsInForStatement, formattingOptions.SpaceAfterSemicolonsInForStatement)
                .WithChangedOption(CSharpFormattingOptions.SpaceBeforeColonInBaseTypeDeclaration, formattingOptions.SpaceBeforeColonInBaseTypeDeclaration)
                .WithChangedOption(CSharpFormattingOptions.SpaceBeforeComma, formattingOptions.SpaceBeforeComma)
                .WithChangedOption(CSharpFormattingOptions.SpaceBeforeDot, formattingOptions.SpaceBeforeDot)
                .WithChangedOption(CSharpFormattingOptions.SpaceBeforeSemicolonsInForStatement, formattingOptions.SpaceBeforeSemicolonsInForStatement)
                .WithChangedOption(CSharpFormattingOptions.SpacingAroundBinaryOperator, BinaryOperatorSpacingOptionForStringValue(formattingOptions.SpacingAroundBinaryOperator))
                .WithChangedOption(CSharpFormattingOptions.IndentBraces, formattingOptions.IndentBraces)
                .WithChangedOption(CSharpFormattingOptions.IndentBlock, formattingOptions.IndentBlock)
                .WithChangedOption(CSharpFormattingOptions.IndentSwitchSection, formattingOptions.IndentSwitchSection)
                .WithChangedOption(CSharpFormattingOptions.IndentSwitchCaseSection, formattingOptions.IndentSwitchCaseSection)
                .WithChangedOption(CSharpFormattingOptions.LabelPositioning, LabelPositionOptionForStringValue(formattingOptions.LabelPositioning))
                .WithChangedOption(CSharpFormattingOptions.WrappingPreserveSingleLine, formattingOptions.WrappingPreserveSingleLine)
                .WithChangedOption(CSharpFormattingOptions.WrappingKeepStatementsOnSingleLine, formattingOptions.WrappingKeepStatementsOnSingleLine)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInTypes, formattingOptions.NewLinesForBracesInTypes)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, formattingOptions.NewLinesForBracesInMethods)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInProperties, formattingOptions.NewLinesForBracesInProperties)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAccessors, formattingOptions.NewLinesForBracesInAccessors)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousMethods, formattingOptions.NewLinesForBracesInAnonymousMethods)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInControlBlocks, formattingOptions.NewLinesForBracesInControlBlocks)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousTypes, formattingOptions.NewLinesForBracesInAnonymousTypes)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInObjectCollectionArrayInitializers, formattingOptions.NewLinesForBracesInObjectCollectionArrayInitializers)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInLambdaExpressionBody, formattingOptions.NewLinesForBracesInLambdaExpressionBody)
                .WithChangedOption(CSharpFormattingOptions.NewLineForElse, formattingOptions.NewLineForElse)
                .WithChangedOption(CSharpFormattingOptions.NewLineForCatch, formattingOptions.NewLineForCatch)
                .WithChangedOption(CSharpFormattingOptions.NewLineForFinally, formattingOptions.NewLineForFinally)
                .WithChangedOption(CSharpFormattingOptions.NewLineForMembersInObjectInit, formattingOptions.NewLineForMembersInObjectInit)
                .WithChangedOption(CSharpFormattingOptions.NewLineForMembersInAnonymousTypes, formattingOptions.NewLineForMembersInAnonymousTypes)
                .WithChangedOption(CSharpFormattingOptions.NewLineForClausesInQuery, formattingOptions.NewLineForClausesInQuery);
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

        private static LabelPositionOptions LabelPositionOptionForStringValue(string value)
        {
            switch (value.ToUpper()) {
                case "LEFTMOST":
                    return LabelPositionOptions.LeftMost;
                case "NOINDENT":
                    return LabelPositionOptions.NoIndent;
                default:
                    return LabelPositionOptions.OneLess;
            }
        }

        private static BinaryOperatorSpacingOptions BinaryOperatorSpacingOptionForStringValue(string value)
        {
            switch (value.ToUpper()) {
                case "IGNORE":
                    return BinaryOperatorSpacingOptions.Ignore;
                case "REMOVE":
                    return BinaryOperatorSpacingOptions.Remove;
                default:
                    return BinaryOperatorSpacingOptions.Single;
            }
        }
    }
}
