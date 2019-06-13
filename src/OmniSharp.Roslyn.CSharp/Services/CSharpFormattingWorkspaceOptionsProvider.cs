using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Options;
using OmniSharp.Options;
using OmniSharp.Roslyn.Options;
using RoslynFormattingOptions = Microsoft.CodeAnalysis.Formatting.FormattingOptions;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IWorkspaceOptionsProvider)), Shared]
    public class CSharpFormattingWorkspaceOptionsProvider : IWorkspaceOptionsProvider
    {
        public int Order => 0;

        private static OptionSet GetOptions(OptionSet optionSet, FormattingOptions formattingOptions)
        {
            return optionSet
                .WithChangedOption(RoslynFormattingOptions.NewLine, LanguageNames.CSharp, formattingOptions.NewLine)
                .WithChangedOption(RoslynFormattingOptions.UseTabs, LanguageNames.CSharp, formattingOptions.UseTabs)
                .WithChangedOption(RoslynFormattingOptions.TabSize, LanguageNames.CSharp, formattingOptions.TabSize)
                .WithChangedOption(RoslynFormattingOptions.IndentationSize, LanguageNames.CSharp, formattingOptions.IndentationSize)
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
                .WithChangedOption(CSharpFormattingOptions.IndentSwitchCaseSectionWhenBlock, formattingOptions.IndentSwitchCaseSectionWhenBlock)
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

        private static LabelPositionOptions LabelPositionOptionForStringValue(string value)
        {
            switch (value.ToUpper())
            {
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
            switch (value.ToUpper())
            {
                case "IGNORE":
                    return BinaryOperatorSpacingOptions.Ignore;
                case "REMOVE":
                    return BinaryOperatorSpacingOptions.Remove;
                default:
                    return BinaryOperatorSpacingOptions.Single;
            }
        }

        public OptionSet Process(OptionSet currentOptionSet, OmniSharpOptions options, IOmniSharpEnvironment omnisharpEnvironment)
        {
            return GetOptions(currentOptionSet, options.FormattingOptions);
        }
    }
}
