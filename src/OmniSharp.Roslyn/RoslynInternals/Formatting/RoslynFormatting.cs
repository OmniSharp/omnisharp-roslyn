#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.RoslynInternals.Formatting
{
    public enum OmniSharpLabelPositionOptions { LeftMost, OneLess, NoIndent }
    public enum OmniSharpBinaryOperatorSpacingOptions { Single, Ignore, Remove }

    public struct OmniSharpLineFormattingOptions
    {
        public bool UseTabs { get; set; }
        public int TabSize { get; set; }
        public int IndentationSize { get; set; }
        public string NewLine { get; set; }
    }

    public readonly struct OmniSharpSyntaxFormattingOptionsWrapper
    {
        internal object UnderlyingObject { get; }
        internal OmniSharpSyntaxFormattingOptionsWrapper(object value) => UnderlyingObject = value;

        public static async ValueTask<OmniSharpSyntaxFormattingOptionsWrapper> FromDocumentAsync(
            Document document, OmniSharpLineFormattingOptions fallbackLineFormattingOptions, CancellationToken cancellationToken)
        {
            var type = RoslynReflection.GetType(
                RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Formatting.SyntaxFormattingOptionsProviders");
            var method = RoslynReflection.GetMethod(type, "GetSyntaxFormattingOptionsAsync",
                m => m.IsStatic && m.GetParameters().Length == 2);
            var value = await RoslynReflection.AwaitResultAsync(
                RoslynReflection.Invoke(method, null, document, cancellationToken)).ConfigureAwait(false);
            return new OmniSharpSyntaxFormattingOptionsWrapper(value);
        }
    }

    public readonly struct OmniSharpOrganizeImportsOptionsWrapper
    {
        internal object UnderlyingObject { get; }

        public OmniSharpOrganizeImportsOptionsWrapper(
            bool placeSystemNamespaceFirst, bool separateImportDirectiveGroups, string newLine)
        {
            var type = RoslynReflection.GetType(
                RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsOptions");
            UnderlyingObject = RoslynReflection.CreateWithProperties(type, null,
                ("PlaceSystemNamespaceFirst", placeSystemNamespaceFirst),
                ("SeparateImportDirectiveGroups", separateImportDirectiveGroups),
                ("NewLine", newLine));
        }

        private OmniSharpOrganizeImportsOptionsWrapper(object value) => UnderlyingObject = value;

        public static async ValueTask<OmniSharpOrganizeImportsOptionsWrapper> FromDocumentAsync(
            Document document, OmniSharpOrganizeImportsOptionsWrapper fallbackOptions, CancellationToken cancellationToken)
        {
            var type = RoslynReflection.GetType(
                RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsOptionsProviders");
            var method = RoslynReflection.GetMethod(type, "GetOrganizeImportsOptionsAsync",
                m => m.IsStatic && m.GetParameters().Length == 2);
            var value = await RoslynReflection.AwaitResultAsync(
                RoslynReflection.Invoke(method, null, document, cancellationToken)).ConfigureAwait(false);
            return new OmniSharpOrganizeImportsOptionsWrapper(value);
        }
    }

    public static class RoslynFormatter
    {
        public static Task<Document> FormatAsync(
            Document document, IEnumerable<TextSpan>? spans, OmniSharpSyntaxFormattingOptionsWrapper options,
            CancellationToken cancellationToken)
        {
            var formatterType = RoslynReflection.GetType(
                RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Formatting.Formatter");
            var method = RoslynReflection.GetMethod(formatterType, "FormatAsync",
                m => m.IsStatic && m.GetParameters().Length == 5 &&
                     m.GetParameters()[0].ParameterType == typeof(Document) &&
                     m.GetParameters()[1].ParameterType == typeof(IEnumerable<TextSpan>) &&
                     m.GetParameters()[2].ParameterType.Name == "SyntaxFormattingOptions");
            return RoslynReflection.Invoke<Task<Document>>(
                method, null, document, spans, options.UnderlyingObject, null, cancellationToken);
        }

        public static async Task<Document> OrganizeImportsAsync(
            Document document, OmniSharpOrganizeImportsOptionsWrapper options, CancellationToken cancellationToken)
        {
            var service = RoslynReflection.GetRequiredLanguageService(
                document, RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.OrganizeImports.IOrganizeImportsService");
            var method = RoslynReflection.GetMethod(service.GetType(), "OrganizeImportsAsync",
                m => !m.IsStatic && m.GetParameters().Length == 3);
            return await RoslynReflection.AwaitResultAsync<Document>(
                RoslynReflection.Invoke(method, service, document, options.UnderlyingObject, cancellationToken))
                .ConfigureAwait(false);
        }
    }

    public static class OmniSharpSyntaxFormattingOptionsFactory
    {
        public static OmniSharpSyntaxFormattingOptionsWrapper Create(
            bool useTabs, int tabSize, int indentationSize, string newLine, bool separateImportDirectiveGroups,
            bool spacingAfterMethodDeclarationName, bool spaceWithinMethodDeclarationParenthesis,
            bool spaceBetweenEmptyMethodDeclarationParentheses, bool spaceAfterMethodCallName,
            bool spaceWithinMethodCallParentheses, bool spaceBetweenEmptyMethodCallParentheses,
            bool spaceAfterControlFlowStatementKeyword, bool spaceWithinExpressionParentheses,
            bool spaceWithinCastParentheses, bool spaceWithinOtherParentheses, bool spaceAfterCast,
            bool spaceBeforeOpenSquareBracket, bool spaceBetweenEmptySquareBrackets, bool spaceWithinSquareBrackets,
            bool spaceAfterColonInBaseTypeDeclaration, bool spaceAfterComma, bool spaceAfterDot,
            bool spaceAfterSemicolonsInForStatement, bool spaceBeforeColonInBaseTypeDeclaration,
            bool spaceBeforeComma, bool spaceBeforeDot, bool spaceBeforeSemicolonsInForStatement,
            OmniSharpBinaryOperatorSpacingOptions spacingAroundBinaryOperator, bool indentBraces, bool indentBlock,
            bool indentSwitchSection, bool indentSwitchCaseSection, bool indentSwitchCaseSectionWhenBlock,
            OmniSharpLabelPositionOptions labelPositioning, bool wrappingPreserveSingleLine,
            bool wrappingKeepStatementsOnSingleLine, bool newLinesForBracesInTypes, bool newLinesForBracesInMethods,
            bool newLinesForBracesInProperties, bool newLinesForBracesInAccessors,
            bool newLinesForBracesInAnonymousMethods, bool newLinesForBracesInControlBlocks,
            bool newLinesForBracesInAnonymousTypes, bool newLinesForBracesInObjectCollectionArrayInitializers,
            bool newLinesForBracesInLambdaExpressionBody, bool newLineForElse, bool newLineForCatch,
            bool newLineForFinally, bool newLineForMembersInObjectInit, bool newLineForMembersInAnonymousTypes,
            bool newLineForClausesInQuery)
        {
            var lineType = RoslynReflection.GetType(
                RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Formatting.LineFormattingOptions");
            var line = RoslynReflection.CreateWithProperties(lineType, null,
                ("UseTabs", useTabs), ("TabSize", tabSize), ("IndentationSize", indentationSize), ("NewLine", newLine));
            var type = RoslynReflection.GetType(
                RoslynReflection.CSharpWorkspacesAssembly, "Microsoft.CodeAnalysis.CSharp.Formatting.CSharpSyntaxFormattingOptions");
            var value = RoslynReflection.CreateWithProperties(type, null,
                ("LineFormatting", line),
                ("SeparateImportDirectiveGroups", separateImportDirectiveGroups),
                ("Spacing", Flags(type.Assembly, "Microsoft.CodeAnalysis.CSharp.Formatting.SpacePlacement",
                    (spacingAfterMethodDeclarationName, "AfterMethodDeclarationName"),
                    (spaceBetweenEmptyMethodDeclarationParentheses, "BetweenEmptyMethodDeclarationParentheses"),
                    (spaceWithinMethodDeclarationParenthesis, "WithinMethodDeclarationParenthesis"),
                    (spaceAfterMethodCallName, "AfterMethodCallName"),
                    (spaceBetweenEmptyMethodCallParentheses, "BetweenEmptyMethodCallParentheses"),
                    (spaceWithinMethodCallParentheses, "WithinMethodCallParentheses"),
                    (spaceAfterControlFlowStatementKeyword, "AfterControlFlowStatementKeyword"),
                    (spaceWithinExpressionParentheses, "WithinExpressionParentheses"),
                    (spaceWithinCastParentheses, "WithinCastParentheses"),
                    (spaceBeforeSemicolonsInForStatement, "BeforeSemicolonsInForStatement"),
                    (spaceAfterSemicolonsInForStatement, "AfterSemicolonsInForStatement"),
                    (spaceWithinOtherParentheses, "WithinOtherParentheses"),
                    (spaceAfterCast, "AfterCast"),
                    (spaceBeforeOpenSquareBracket, "BeforeOpenSquareBracket"),
                    (spaceBetweenEmptySquareBrackets, "BetweenEmptySquareBrackets"),
                    (spaceWithinSquareBrackets, "WithinSquareBrackets"),
                    (spaceAfterColonInBaseTypeDeclaration, "AfterColonInBaseTypeDeclaration"),
                    (spaceBeforeColonInBaseTypeDeclaration, "BeforeColonInBaseTypeDeclaration"),
                    (spaceAfterComma, "AfterComma"), (spaceBeforeComma, "BeforeComma"),
                    (spaceAfterDot, "AfterDot"), (spaceBeforeDot, "BeforeDot"))),
                ("SpacingAroundBinaryOperator", EnumValue(type.Assembly,
                    "Microsoft.CodeAnalysis.CSharp.Formatting.BinaryOperatorSpacingOptionsInternal",
                    spacingAroundBinaryOperator.ToString())),
                ("NewLines", Flags(type.Assembly, "Microsoft.CodeAnalysis.CSharp.Formatting.NewLinePlacement",
                    (newLineForMembersInObjectInit, "BeforeMembersInObjectInitializers"),
                    (newLineForMembersInAnonymousTypes, "BeforeMembersInAnonymousTypes"),
                    (newLineForElse, "BeforeElse"), (newLineForCatch, "BeforeCatch"),
                    (newLineForFinally, "BeforeFinally"), (newLinesForBracesInTypes, "BeforeOpenBraceInTypes"),
                    (newLinesForBracesInAnonymousTypes, "BeforeOpenBraceInAnonymousTypes"),
                    (newLinesForBracesInObjectCollectionArrayInitializers, "BeforeOpenBraceInObjectCollectionArrayInitializers"),
                    (newLinesForBracesInProperties, "BeforeOpenBraceInProperties"),
                    (newLinesForBracesInMethods, "BeforeOpenBraceInMethods"),
                    (newLinesForBracesInAccessors, "BeforeOpenBraceInAccessors"),
                    (newLinesForBracesInAnonymousMethods, "BeforeOpenBraceInAnonymousMethods"),
                    (newLinesForBracesInLambdaExpressionBody, "BeforeOpenBraceInLambdaExpressionBody"),
                    (newLinesForBracesInControlBlocks, "BeforeOpenBraceInControlBlocks"),
                    (newLineForClausesInQuery, "BetweenQueryExpressionClauses"))),
                ("LabelPositioning", EnumValue(type.Assembly,
                    "Microsoft.CodeAnalysis.CSharp.Formatting.LabelPositionOptionsInternal", labelPositioning.ToString())),
                ("Indentation", Flags(type.Assembly, "Microsoft.CodeAnalysis.CSharp.Formatting.IndentationPlacement",
                    (indentBraces, "Braces"), (indentBlock, "BlockContents"),
                    (indentSwitchCaseSection, "SwitchCaseContents"),
                    (indentSwitchCaseSectionWhenBlock, "SwitchCaseContentsWhenBlock"),
                    (indentSwitchSection, "SwitchSection"))),
                ("WrappingKeepStatementsOnSingleLine", wrappingKeepStatementsOnSingleLine),
                ("WrappingPreserveSingleLine", wrappingPreserveSingleLine));
            return new OmniSharpSyntaxFormattingOptionsWrapper(value);
        }

        private static object EnumValue(Assembly assembly, string typeName, string name)
            => Enum.Parse(assembly.GetType(typeName, true)!, name);

        private static object Flags(Assembly assembly, string typeName, params (bool Enabled, string Name)[] values)
        {
            var type = assembly.GetType(typeName, true)!;
            long result = 0;
            foreach (var value in values.Where(v => v.Enabled))
                result |= Convert.ToInt64(Enum.Parse(type, value.Name));
            return Enum.ToObject(type, result);
        }
    }

}
