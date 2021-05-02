namespace OmniSharp.Options
{
    // TODO: These are mostly C#-specific. Should they be moved into in OmniSharp.Roslyn.CSharp somehow?
    public class FormattingOptions
    {
        public FormattingOptions()
        {
            //just defaults
            NewLine = "\n";
            UseTabs = false;
            TabSize = 4;
            IndentationSize = 4;
            SpacingAfterMethodDeclarationName = false;
            SpaceWithinMethodDeclarationParenthesis = false;
            SpaceBetweenEmptyMethodDeclarationParentheses = false;
            SpaceAfterMethodCallName = false;
            SpaceWithinMethodCallParentheses = false;
            SpaceBetweenEmptyMethodCallParentheses = false;
            SpaceAfterControlFlowStatementKeyword = true;
            SpaceWithinExpressionParentheses = false;
            SpaceWithinCastParentheses = false;
            SpaceWithinOtherParentheses = false;
            SpaceAfterCast = false;
            SpacesIgnoreAroundVariableDeclaration = false;
            SpaceBeforeOpenSquareBracket = false;
            SpaceBetweenEmptySquareBrackets = false;
            SpaceWithinSquareBrackets = false;
            SpaceAfterColonInBaseTypeDeclaration = true;
            SpaceAfterComma = true;
            SpaceAfterDot = false;
            SpaceAfterSemicolonsInForStatement = true;
            SpaceBeforeColonInBaseTypeDeclaration = true;
            SpaceBeforeComma = false;
            SpaceBeforeDot = false;
            SpaceBeforeSemicolonsInForStatement = false;
            SpacingAroundBinaryOperator = "single";
            IndentBraces = false;
            IndentBlock = true;
            IndentSwitchSection = true;
            IndentSwitchCaseSection = true;
            IndentSwitchCaseSectionWhenBlock = true;
            LabelPositioning = "oneLess";
            WrappingPreserveSingleLine = true;
            WrappingKeepStatementsOnSingleLine = true;
            NewLinesForBracesInTypes = true;
            NewLinesForBracesInMethods = true;
            NewLinesForBracesInProperties = true;
            NewLinesForBracesInAccessors = true;
            NewLinesForBracesInAnonymousMethods = true;
            NewLinesForBracesInControlBlocks = true;
            NewLinesForBracesInAnonymousTypes = true;
            NewLinesForBracesInObjectCollectionArrayInitializers = true;
            NewLinesForBracesInLambdaExpressionBody = true;
            NewLineForElse = true;
            NewLineForCatch = true;
            NewLineForFinally = true;
            NewLineForMembersInObjectInit = true;
            NewLineForMembersInAnonymousTypes = true;
            NewLineForClausesInQuery = true;
        }

        public bool OrganizeImports { get; set; }

        public bool EnableEditorConfigSupport { get; set; }

        public string NewLine { get; set; }

        public bool UseTabs { get; set; }

        public int TabSize { get; set; }

        public int IndentationSize { get; set; }

        public bool SpacingAfterMethodDeclarationName { get; set; }

        public bool SpaceWithinMethodDeclarationParenthesis { get; set; }

        public bool SpaceBetweenEmptyMethodDeclarationParentheses { get; set; }

        public bool SpaceAfterMethodCallName { get; set; }

        public bool SpaceWithinMethodCallParentheses { get; set; }

        public bool SpaceBetweenEmptyMethodCallParentheses { get; set; }

        public bool SpaceAfterControlFlowStatementKeyword { get; set; }

        public bool SpaceWithinExpressionParentheses { get; set; }

        public bool SpaceWithinCastParentheses { get; set; }

        public bool SpaceWithinOtherParentheses { get; set; }

        public bool SpaceAfterCast { get; set; }

        public bool SpacesIgnoreAroundVariableDeclaration { get; set; }

        public bool SpaceBeforeOpenSquareBracket { get; set; }

        public bool SpaceBetweenEmptySquareBrackets { get; set; }

        public bool SpaceWithinSquareBrackets { get; set; }

        public bool SpaceAfterColonInBaseTypeDeclaration { get; set; }

        public bool SpaceAfterComma { get; set; }

        public bool SpaceAfterDot { get; set; }

        public bool SpaceAfterSemicolonsInForStatement { get; set; }

        public bool SpaceBeforeColonInBaseTypeDeclaration { get; set; }

        public bool SpaceBeforeComma { get; set; }

        public bool SpaceBeforeDot { get; set; }

        public bool SpaceBeforeSemicolonsInForStatement { get; set; }

        public string SpacingAroundBinaryOperator { get; set; }

        public bool IndentBraces { get; set; }

        public bool IndentBlock { get; set; }

        public bool IndentSwitchSection { get; set; }

        public bool IndentSwitchCaseSection { get; set; }

        public bool IndentSwitchCaseSectionWhenBlock { get; set; }

        public string LabelPositioning { get; set; }

        public bool WrappingPreserveSingleLine { get; set; }

        public bool WrappingKeepStatementsOnSingleLine { get; set; }

        public bool NewLinesForBracesInTypes { get; set; }

        public bool NewLinesForBracesInMethods { get; set; }

        public bool NewLinesForBracesInProperties { get; set; }

        public bool NewLinesForBracesInAccessors { get; set; }

        public bool NewLinesForBracesInAnonymousMethods { get; set; }

        public bool NewLinesForBracesInControlBlocks { get; set; }

        public bool NewLinesForBracesInAnonymousTypes { get; set; }

        public bool NewLinesForBracesInObjectCollectionArrayInitializers { get; set; }

        public bool NewLinesForBracesInLambdaExpressionBody { get; set; }

        public bool NewLineForElse { get; set; }

        public bool NewLineForCatch { get; set; }

        public bool NewLineForFinally { get; set; }

        public bool NewLineForMembersInObjectInit { get; set; }

        public bool NewLineForMembersInAnonymousTypes { get; set; }

        public bool NewLineForClausesInQuery { get; set; }

    }
}
