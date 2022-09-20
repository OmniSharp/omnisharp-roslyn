using Microsoft.CodeAnalysis;

namespace OmniSharp
{
    public static class ReturnTypeFormatter
    {
        public static string GetReturnType(ISymbol symbol)
        {
            var type = GetReturnTypeSymbol(symbol);
            return type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        private static ITypeSymbol GetReturnTypeSymbol(ISymbol symbol)
        {
            return symbol switch
            {
                IMethodSymbol methodSymbol when methodSymbol.MethodKind != MethodKind.Constructor
                        => methodSymbol.ReturnType,
                IPropertySymbol propertySymbol => propertySymbol.Type,
                ILocalSymbol localSymbol => localSymbol.Type,
                IParameterSymbol parameterSymbol => parameterSymbol.Type,
                IFieldSymbol fieldSymbol => fieldSymbol.Type,
                IEventSymbol eventSymbol => eventSymbol.Type,
                _ => null
            };
        }
    }
}
