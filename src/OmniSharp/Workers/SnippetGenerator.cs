using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OmniSharp
{
    public class SnippetGenerator
    {
        private bool _includeMarkers;
        private int _counter = 1;
        private StringBuilder _sb = new StringBuilder();
        private SymbolDisplayFormat _format;

        public SnippetGenerator(bool includeMarkers)
        {
            _includeMarkers = includeMarkers;
        }

        public string GenerateSnippet(ISymbol symbol)
        {
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
                // render everything starting from the opening parens
                RenderDisplayParts(symbol, parts.SkipWhile(p => p.Kind != SymbolDisplayPartKind.Punctuation));
            }
            else
            {
                var symbolKind = symbol.Kind;
                if (symbol.Kind == SymbolKind.Method)
                {
                    RenderMethodSymbol(symbol as IMethodSymbol);
                }
                else if (symbol.Kind == SymbolKind.Local || symbol.Kind == SymbolKind.Parameter)
                {
                    _sb.Append(symbol.Name);
                }
                else
                {
                    var parts = symbol.ToDisplayParts(_format);
                    RenderDisplayParts(symbol, parts);
                }
            }
            if (_includeMarkers)
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
                    if (arg != last)
                    {
                        _sb.Append(", ");
                    }
                }
                _sb.Append(">");
            }
            var parts = methodSymbol.ToDisplayParts(_format);
            // render everything starting from the opening parens
            RenderDisplayParts(methodSymbol, parts.SkipWhile(p => p.ToString() != "("));
            if (methodSymbol.ReturnsVoid)
            {
                _sb.Append(";");
            }
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
                if (arg == param)
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
            foreach (var p in methodSymbol.Parameters)
            {
                var types = ExplodeTypes(p.Type);
                foreach (var t in types)
                {
                    yield return t;
                }
            }
        }

        private IEnumerable<ISymbol> ExplodeTypes(ISymbol symbol)
        {
            var t = symbol as INamedTypeSymbol;
            if (t != null)
            {
                var typeParams = t.TypeArguments;

                foreach (var tp in typeParams)
                {
                    var e = ExplodeTypes(tp);
                    foreach (var et in e)
                    {
                        yield return et;
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
            if (_includeMarkers)
            {
                _sb.Append("${");
                _sb.Append(_counter++);
                _sb.Append(":");
            }
        }

        private void RenderSnippetEndMarker()
        {
            if (_includeMarkers)
            {
                _sb.Append("}");
            }
        }

        private void RenderDisplayParts(ISymbol symbol, IEnumerable<SymbolDisplayPart> parts)
        {
            bool writingParameter = false;
            var lastPart = default(SymbolDisplayPart);

            foreach (var part in parts)
            {
                if (!writingParameter && part.Kind == SymbolDisplayPartKind.TypeParameterName)
                {
                    RenderSnippetStartMarker();
                    _sb.Append(part.ToString());
                    RenderSnippetEndMarker();
                }
                else
                {
                    string displayPart = part.ToString();
                    var methodSymbol = symbol as IMethodSymbol;
                    if (methodSymbol != null && methodSymbol.Parameters.Any())
                    {
                        if (part.Kind == SymbolDisplayPartKind.Punctuation &&
                            displayPart == "(")
                        {
                            _sb.Append(displayPart);
                            RenderSnippetStartMarker();
                            writingParameter = true;
                        }
                        else if (writingParameter &&
                            part.Kind == SymbolDisplayPartKind.Punctuation
                                 && (displayPart == ")" || (displayPart == "," && lastPart.Kind == SymbolDisplayPartKind.ParameterName)))
                        {
                            RenderSnippetEndMarker();
                            _sb.Append(displayPart);
                            writingParameter = false;
                        }
                        else
                        {
                            _sb.Append(displayPart);
                        }

                        if (displayPart == " " && !writingParameter)
                        {
                            RenderSnippetStartMarker();
                            writingParameter = true;
                        }
                    }
                    else
                    {
                        _sb.Append(displayPart);
                    }
                }
                lastPart = part;
            }
        }
    }
}