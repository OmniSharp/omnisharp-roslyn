using System;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Extensions
{
    public static class SymbolExtensions
    {
        public static string GetKind(this ISymbol symbol)
        {
            if (symbol is INamedTypeSymbol namedType)
            {
                return Enum.GetName(namedType.TypeKind.GetType(), namedType.TypeKind);
            }

            if (symbol.Kind == SymbolKind.Field &&
                symbol.ContainingType?.TypeKind == TypeKind.Enum &&
                symbol.Name != WellKnownMemberNames.EnumBackingFieldName)
            {
                return "EnumMember";
            }

            if ((symbol as IFieldSymbol)?.IsConst == true)
            {
                return "Const";
            }

            return Enum.GetName(symbol.Kind.GetType(), symbol.Kind);
        }

        internal static INamedTypeSymbol GetTopLevelContainingNamedType(this ISymbol symbol)
        {
            // Traverse up until we find a named type that is parented by the namespace
            var topLevelNamedType = symbol;
            while (topLevelNamedType.ContainingSymbol != symbol.ContainingNamespace ||
                topLevelNamedType.Kind != SymbolKind.NamedType)
            {
                topLevelNamedType = topLevelNamedType.ContainingSymbol;
            }

            return (INamedTypeSymbol)topLevelNamedType;
        }
    }
}
