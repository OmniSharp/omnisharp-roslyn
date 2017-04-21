using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.Utilities
{
    public static class ISymbolExtensions
    {
        private readonly static CachedStringBuilder s_cachedBuilder;

        public static string GetMetadataName(this ISymbol symbol)
        {
            if (symbol == null)
            {
                throw new ArgumentNullException(nameof(symbol));
            }

            var symbols = new Stack<ISymbol>();

            while (symbol != null)
            {
                if (symbol.Kind == SymbolKind.Assembly ||
                    symbol.Kind == SymbolKind.NetModule)
                {
                    break;
                }

                if ((symbol as INamespaceSymbol)?.IsGlobalNamespace == true)
                {
                    break;
                }

                symbols.Push(symbol);
                symbol = symbol.ContainingSymbol;
            }

            var builder = s_cachedBuilder.Acquire();
            try
            {
                ISymbol current = null, previous = null;

                while (symbols.Count > 0)
                {
                    current = symbols.Pop();

                    if (previous != null)
                    {
                        if (previous.Kind == SymbolKind.NamedType &&
                            current.Kind == SymbolKind.NamedType)
                        {
                            builder.Append('+');
                        }
                        else
                        {
                            builder.Append('.');
                        }
                    }

                    builder.Append(current.MetadataName);

                    previous = current;
                }

                return builder.ToString();
            }
            finally
            {
                s_cachedBuilder.Release(builder);
            }
        }
    }
}
