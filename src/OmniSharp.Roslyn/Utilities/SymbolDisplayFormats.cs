using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn.Utilities
{
    public static class SymbolDisplayFormats
    {
        public static readonly SymbolDisplayFormat ShortTypeFormat = new SymbolDisplayFormat(
         typeQualificationStyle:
             SymbolDisplayTypeQualificationStyle.NameOnly,
         genericsOptions:
             SymbolDisplayGenericsOptions.IncludeTypeParameters);

        public static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
            typeQualificationStyle:
                SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions:
                SymbolDisplayGenericsOptions.IncludeTypeParameters |
                SymbolDisplayGenericsOptions.IncludeVariance);

        public static readonly SymbolDisplayFormat ShortMemberFormat = new SymbolDisplayFormat(
            genericsOptions:
                SymbolDisplayGenericsOptions.IncludeTypeParameters);

        public static readonly SymbolDisplayFormat MemberFormat = new SymbolDisplayFormat(
            genericsOptions:
                SymbolDisplayGenericsOptions.IncludeTypeParameters |
                SymbolDisplayGenericsOptions.IncludeVariance,
            memberOptions:
                SymbolDisplayMemberOptions.IncludeParameters,
            parameterOptions:
                SymbolDisplayParameterOptions.IncludeDefaultValue |
                SymbolDisplayParameterOptions.IncludeExtensionThis |
                SymbolDisplayParameterOptions.IncludeName |
                SymbolDisplayParameterOptions.IncludeParamsRefOut |
                SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
    }
}
