#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.CSharp.Formatting;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.DocumentationComments;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Formatting;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Options;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.Utilities;

namespace OmniSharp.Roslyn.CSharp.Workers.Formatting
{
    public static class FormattingWorker
    {
        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattingChangesAfterKeystroke(Document document, int position, char character, OmniSharpOptions omnisharpOptions)
        {
            if (await GetDocumentationCommentChanges(document, position, character, omnisharpOptions) is LinePositionSpanTextChange change)
            {
                return new[] { change };
            }

            if (character == '}' || character == ';')
            {
                // format after ; and }
                var root = await document.GetSyntaxRootAsync();
                Debug.Assert(root != null);
                var node = FindFormatTarget(root!, position);
                if (node != null)
                {
                    return await GetFormattingChanges(document, node.FullSpan.Start, node.FullSpan.End, omnisharpOptions);
                }
            }

            return Enumerable.Empty<LinePositionSpanTextChange>();
        }

        public static SyntaxNode? FindFormatTarget(SyntaxNode root, int position)
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

        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattingChanges(Document document, int start, int end, OmniSharpOptions omnisharpOptions)
        {
            var newDocument = await FormatDocument(document, omnisharpOptions, TextSpan.FromBounds(start, end));
            return await TextChanges.GetAsync(newDocument, document);
        }

        public static async Task<string> GetFormattedText(Document document, OmniSharpOptions omnisharpOptions)
        {
            var newDocument = await FormatDocument(document, omnisharpOptions);
            var text = await newDocument.GetTextAsync();
            return text.ToString();
        }

        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetFormattedTextChanges(Document document, OmniSharpOptions omnisharpOptions)
        {
            var newDocument = await FormatDocument(document, omnisharpOptions);
            return await TextChanges.GetAsync(newDocument, document);
        }

        private static async Task<Document> FormatDocument(Document document, OmniSharpOptions omnisharpOptions, TextSpan? textSpan = null)
        {
            var spans = (textSpan != null) ? new[] { textSpan.Value } : null;
            var formattingOtions = await GetFormattingOptionsAsync(document, omnisharpOptions);
            var newDocument = await OmniSharpFormatter.FormatAsync(document, spans, formattingOtions, CancellationToken.None);
            if (omnisharpOptions.FormattingOptions.OrganizeImports)
            {
                var organizeImportsOptions = await GetOrganizeImportsOptionsAsync(document, omnisharpOptions);
                newDocument = await OmniSharpFormatter.OrganizeImportsAsync(newDocument, organizeImportsOptions, CancellationToken.None);
            }

            return newDocument;
        }

        // If we are not using .editorconfig for formatting options then we can avoid any overhead of calculating document options.
        internal static async ValueTask<OmniSharpOrganizeImportsOptionsWrapper> GetOrganizeImportsOptionsAsync(Document document, OmniSharpOptions omnisharpOptions)
        {
            var fallbackOptions = WrapOrganizeImportsOptions(omnisharpOptions.FormattingOptions);
            return omnisharpOptions.FormattingOptions.EnableEditorConfigSupport
                        ? await OmniSharpOrganizeImportsOptionsWrapper.FromDocumentAsync(document, fallbackOptions, CancellationToken.None)
                        : fallbackOptions;
        }

        // If we are not using .editorconfig for formatting options then we can avoid any overhead of calculating document options.
        internal static async ValueTask<OmniSharpSyntaxFormattingOptionsWrapper> GetFormattingOptionsAsync(Document document, OmniSharpOptions omnisharpOptions)
        {
            var fallbackOptions = CreateLineFormattingOptions(omnisharpOptions.FormattingOptions);
            return omnisharpOptions.FormattingOptions.EnableEditorConfigSupport
                        ? await OmniSharpSyntaxFormattingOptionsWrapper.FromDocumentAsync(document, fallbackOptions, CancellationToken.None)
                        : WrapFormattingOptions(omnisharpOptions.FormattingOptions);
        }

        // If we are not using .editorconfig for formatting options then we can avoid any overhead of calculating document options.
        internal static async ValueTask<OmniSharpDocumentationCommentOptionsWrapper> GetDocumentationCommentOptionsAsync(Document document, OmniSharpOptions omnisharpOptions)
        {
            return omnisharpOptions.FormattingOptions.EnableEditorConfigSupport
                        ? await OmniSharpDocumentationCommentOptionsWrapper.FromDocumentAsync(document, autoXmlDocCommentGeneration: true, CancellationToken.None)
                        : WrapDocumentationCommentOptions(omnisharpOptions.FormattingOptions);
        }

        private static OmniSharpOrganizeImportsOptionsWrapper WrapOrganizeImportsOptions(OmniSharp.Options.FormattingOptions options)
           => new(
               placeSystemNamespaceFirst: true,
               separateImportDirectiveGroups: false,
               options.NewLine);

        private static OmniSharpSyntaxFormattingOptionsWrapper WrapFormattingOptions(OmniSharp.Options.FormattingOptions options)
            => OmniSharpSyntaxFormattingOptionsFactory.Create(
                useTabs: options.UseTabs,
                tabSize: options.TabSize,
                indentationSize: options.IndentationSize,
                newLine: options.NewLine,
                separateImportDirectiveGroups: options.SeparateImportDirectiveGroups,
                spacingAfterMethodDeclarationName: options.SpacingAfterMethodDeclarationName,
                spaceWithinMethodDeclarationParenthesis: options.SpaceWithinMethodDeclarationParenthesis,
                spaceBetweenEmptyMethodDeclarationParentheses: options.SpaceBetweenEmptyMethodDeclarationParentheses,
                spaceAfterMethodCallName: options.SpaceAfterMethodCallName,
                spaceWithinMethodCallParentheses: options.SpaceWithinMethodCallParentheses,
                spaceBetweenEmptyMethodCallParentheses: options.SpaceBetweenEmptyMethodCallParentheses,
                spaceAfterControlFlowStatementKeyword: options.SpaceAfterControlFlowStatementKeyword,
                spaceWithinExpressionParentheses: options.SpaceWithinExpressionParentheses,
                spaceWithinCastParentheses: options.SpaceWithinCastParentheses,
                spaceWithinOtherParentheses: options.SpaceWithinOtherParentheses,
                spaceAfterCast: options.SpaceAfterCast,
                spaceBeforeOpenSquareBracket: options.SpaceBeforeOpenSquareBracket,
                spaceBetweenEmptySquareBrackets: options.SpaceBetweenEmptySquareBrackets,
                spaceWithinSquareBrackets: options.SpaceWithinSquareBrackets,
                spaceAfterColonInBaseTypeDeclaration: options.SpaceAfterColonInBaseTypeDeclaration,
                spaceAfterComma: options.SpaceAfterComma,
                spaceAfterDot: options.SpaceAfterDot,
                spaceAfterSemicolonsInForStatement: options.SpaceAfterSemicolonsInForStatement,
                spaceBeforeColonInBaseTypeDeclaration: options.SpaceBeforeColonInBaseTypeDeclaration,
                spaceBeforeComma: options.SpaceBeforeComma,
                spaceBeforeDot: options.SpaceBeforeDot,
                spaceBeforeSemicolonsInForStatement: options.SpaceBeforeSemicolonsInForStatement,
                spacingAroundBinaryOperator: BinaryOperatorSpacingOptionForStringValue(options.SpacingAroundBinaryOperator),
                indentBraces: options.IndentBraces,
                indentBlock: options.IndentBlock,
                indentSwitchSection: options.IndentSwitchSection,
                indentSwitchCaseSection: options.IndentSwitchCaseSection,
                indentSwitchCaseSectionWhenBlock: options.IndentSwitchCaseSectionWhenBlock,
                labelPositioning: LabelPositionOptionForStringValue(options.LabelPositioning),
                wrappingPreserveSingleLine: options.WrappingPreserveSingleLine,
                wrappingKeepStatementsOnSingleLine: options.WrappingKeepStatementsOnSingleLine,
                newLinesForBracesInTypes: options.NewLinesForBracesInTypes,
                newLinesForBracesInMethods: options.NewLinesForBracesInMethods,
                newLinesForBracesInProperties: options.NewLinesForBracesInProperties,
                newLinesForBracesInAccessors: options.NewLinesForBracesInAccessors,
                newLinesForBracesInAnonymousMethods: options.NewLinesForBracesInAnonymousMethods,
                newLinesForBracesInControlBlocks: options.NewLinesForBracesInControlBlocks,
                newLinesForBracesInAnonymousTypes: options.NewLinesForBracesInAnonymousTypes,
                newLinesForBracesInObjectCollectionArrayInitializers: options.NewLinesForBracesInObjectCollectionArrayInitializers,
                newLinesForBracesInLambdaExpressionBody: options.NewLinesForBracesInLambdaExpressionBody,
                newLineForElse: options.NewLineForElse,
                newLineForCatch: options.NewLineForCatch,
                newLineForFinally: options.NewLineForFinally,
                newLineForMembersInObjectInit: options.NewLineForMembersInObjectInit,
                newLineForMembersInAnonymousTypes: options.NewLineForMembersInAnonymousTypes,
                newLineForClausesInQuery: options.NewLineForClausesInQuery);

        private static OmniSharpDocumentationCommentOptionsWrapper WrapDocumentationCommentOptions(OmniSharp.Options.FormattingOptions options)
          => new(autoXmlDocCommentGeneration: true, CreateLineFormattingOptions(options));

        private static OmniSharpLineFormattingOptions CreateLineFormattingOptions(OmniSharp.Options.FormattingOptions options)
            => new()
            {
                IndentationSize = options.IndentationSize,
                TabSize = options.TabSize,
                UseTabs = options.UseTabs,
                NewLine = options.NewLine,
            };

        internal static OmniSharpLabelPositionOptions LabelPositionOptionForStringValue(string value)
            => value.ToUpper() switch
            {
                "LEFTMOST" => OmniSharpLabelPositionOptions.LeftMost,
                "NOINDENT" => OmniSharpLabelPositionOptions.NoIndent,
                _ => OmniSharpLabelPositionOptions.OneLess,
            };

        internal static OmniSharpBinaryOperatorSpacingOptions BinaryOperatorSpacingOptionForStringValue(string value)
            => value.ToUpper() switch
            {
                "IGNORE" => OmniSharpBinaryOperatorSpacingOptions.Ignore,
                "REMOVE" => OmniSharpBinaryOperatorSpacingOptions.Remove,
                _ => OmniSharpBinaryOperatorSpacingOptions.Single,
            };

        private static async Task<LinePositionSpanTextChange?> GetDocumentationCommentChanges(Document document, int position, char character, OmniSharpOptions omnisharpOptions)
        {
            if (character != '\n' && character != '/')
            {
                return null;
            }

            var text = await document.GetTextAsync();
            var syntaxTree = await document.GetSyntaxTreeAsync();

            var docCommentOptions = await GetDocumentationCommentOptionsAsync(document, omnisharpOptions).ConfigureAwait(false);

            var snippet = character == '\n' ?
                OmniSharpDocumentationCommentsSnippetService.GetDocumentationCommentSnippetOnEnterTyped(document, syntaxTree!, text, position, docCommentOptions, CancellationToken.None) :
                OmniSharpDocumentationCommentsSnippetService.GetDocumentationCommentSnippetOnCharacterTyped(document, syntaxTree!, text, position, docCommentOptions, CancellationToken.None);

            if (snippet == null)
            {
                return null;
            }
            else
            {
                var range = text.GetRangeFromSpan(snippet.SpanToReplace);
                return new LinePositionSpanTextChange
                {
                    NewText = snippet.SnippetText,
                    StartLine = range.Start.Line,
                    StartColumn = range.Start.Column,
                    EndLine = range.End.Line,
                    EndColumn = range.End.Column
                };
            }
        }
    }
}
