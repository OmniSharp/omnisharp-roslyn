using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace OmniSharp
{
    public class SnippetGenerator
    {
        private int _counter = 1;
        private StringBuilder _sb;
        private SymbolDisplayFormat _format;

        public bool IncludeMarkers { get; set; }
        public bool IncludeOptionalParameters { get; set; }

        public string Generate(ISymbol symbol)
        {
            _sb = new StringBuilder();
            _format = SymbolDisplayFormat.MinimallyQualifiedFormat;
            _format = _format.WithMemberOptions(_format.MemberOptions
                                                ^ SymbolDisplayMemberOptions.IncludeContainingType
                                                ^ SymbolDisplayMemberOptions.IncludeType);

            if (IsConstructor(symbol))
            {
                // only the containing type contains the type parameters
                var parts = symbol.ContainingType.ToDisplayParts(_format);
                RenderDisplayParts(symbol, parts);
                parts = symbol.ToDisplayParts(_format);
                RenderParameters(symbol as IMethodSymbol);
            }
            else
            {
                var symbolKind = symbol.Kind;
                if (symbol.Kind == SymbolKind.Method)
                {
                    RenderMethodSymbol(symbol as IMethodSymbol);
                }
                else if (symbol.Kind == SymbolKind.Event ||
                         symbol.Kind == SymbolKind.Local ||
                         symbol.Kind == SymbolKind.Parameter)
                {
                    _sb.Append(symbol.Name);
                }
                else
                {
                    var parts = symbol.ToDisplayParts(_format);
                    RenderDisplayParts(symbol, parts);
                }
            }

            if (IncludeMarkers)
            {
                _sb.Append("$0");
            }

            return _sb.ToString();
        }

        private void RenderMethodSymbol(IMethodSymbol methodSymbol)
        {
            var nonInferredTypeArguments = NonInferredTypeArguments(methodSymbol);
            _sb.Append(methodSymbol.Name);

            if (nonInferredTypeArguments.Any())
            {
                _sb.Append("<");
                var last = nonInferredTypeArguments.Last();
                foreach (var arg in nonInferredTypeArguments)
                {
                    RenderSnippetStartMarker();
                    _sb.Append(arg);
                    RenderSnippetEndMarker();

                    if (!Equals(arg, last))
                    {
                        _sb.Append(", ");
                    }
                }
                _sb.Append(">");
            }

            RenderParameters(methodSymbol);
            if (methodSymbol.ReturnsVoid && IncludeMarkers)
            {
                _sb.Append(";");
            }
        }

        private void RenderParameters(IMethodSymbol methodSymbol)
        {
            IEnumerable<IParameterSymbol> parameters = methodSymbol.Parameters;

            if (!IncludeOptionalParameters)
            {
                parameters = parameters.Where(p => !p.IsOptional);
            }
            _sb.Append("(");

            if (parameters.Any())
            {
                var last = parameters.Last();
                foreach (var parameter in parameters)
                {
                    RenderSnippetStartMarker();
                    _sb.Append(parameter.ToDisplayString(_format));
                    RenderSnippetEndMarker();

                    if (!Equals(parameter, last))
                    {
                        _sb.Append(", ");
                    }
                }
            }
            _sb.Append(")");
        }

        private IEnumerable<ISymbol> NonInferredTypeArguments(IMethodSymbol methodSymbol)
        {
            var typeParameters = methodSymbol.TypeParameters;
            var typeArguments = methodSymbol.TypeArguments;

            var nonInferredTypeArguments = new List<ISymbol>();

            for (int i = 0; i < typeParameters.Count(); i++)
            {
                var arg = typeArguments[i];
                var param = typeParameters[i];
                if (Equals(arg, param))
                {
                    // this type parameter has not been resolved
                    nonInferredTypeArguments.Add(arg);
                }
            }

            // We might have more inferred types once the method parameters have
            // been supplied. Remove these.
            var parameterTypes = ParameterTypes(methodSymbol);
            return nonInferredTypeArguments.Except(parameterTypes);
        }

        private IEnumerable<ISymbol> ParameterTypes(IMethodSymbol methodSymbol)
        {
            foreach (var parameter in methodSymbol.Parameters)
            {
                var types = ExplodeTypes(parameter.Type);
                foreach (var type in types)
                {
                    yield return type;
                }
            }
        }

        private IEnumerable<ISymbol> ExplodeTypes(ISymbol symbol)
        {
            var typeSymbol = symbol as INamedTypeSymbol;
            if (typeSymbol != null)
            {
                var typeParams = typeSymbol.TypeArguments;

                foreach (var typeParam in typeParams)
                {
                    var explodedTypes = ExplodeTypes(typeParam);
                    foreach (var type in explodedTypes)
                    {
                        yield return type;
                    }
                }
            }
            yield return symbol;
        }

        private bool IsConstructor(ISymbol symbol)
        {
            var methodSymbol = symbol as IMethodSymbol;
            return methodSymbol != null && methodSymbol.MethodKind == MethodKind.Constructor;
        }

        private void RenderSnippetStartMarker()
        {
            if (IncludeMarkers)
            {
                _sb.Append("${");
                _sb.Append(_counter++);
                _sb.Append(":");
            }
        }

        private void RenderSnippetEndMarker()
        {
            if (IncludeMarkers)
            {
                _sb.Append("}");
            }
        }

        private void RenderDisplayParts(ISymbol symbol, IEnumerable<SymbolDisplayPart> parts)
        {
            foreach (var part in parts)
            {
                if (part.Kind == SymbolDisplayPartKind.TypeParameterName)
                {
                    RenderSnippetStartMarker();
                    _sb.Append(part.ToString());
                    RenderSnippetEndMarker();
                }
                else
                {
                    _sb.Append(part.ToString());
                }
            }
        }
    }
}
