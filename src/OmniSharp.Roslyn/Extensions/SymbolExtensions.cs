using System;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Extensions
{
    public static class SymbolExtensions
    {
        public static string GetKind(this ISymbol symbol)
        {
            var namedType = symbol as INamedTypeSymbol;
            if (namedType != null)
            {
                return Enum.GetName(namedType.TypeKind.GetType(), namedType.TypeKind);
            }
            return Enum.GetName(symbol.Kind.GetType(), symbol.Kind);
        }
    }
}
