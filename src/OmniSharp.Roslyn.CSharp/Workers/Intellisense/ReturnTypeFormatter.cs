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
            var methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                if (methodSymbol.MethodKind != MethodKind.Constructor)
                {
                    return methodSymbol.ReturnType;
                }
            }

            var propertySymbol = symbol as IPropertySymbol;
            if (propertySymbol != null)
            {
                return propertySymbol.Type;
            }

            var localSymbol = symbol as ILocalSymbol;
            if (localSymbol != null)
            {
                return localSymbol.Type;
            }

            var parameterSymbol = symbol as IParameterSymbol;
            if (parameterSymbol != null)
            {
                return parameterSymbol.Type;
            }

            var fieldSymbol = symbol as IFieldSymbol;
            if (fieldSymbol != null)
            {
                return fieldSymbol.Type;
            }

            var eventSymbol = symbol as IEventSymbol;
            if (eventSymbol != null)
            {
                return eventSymbol.Type;
            }

            return null;
        }
    }
}
