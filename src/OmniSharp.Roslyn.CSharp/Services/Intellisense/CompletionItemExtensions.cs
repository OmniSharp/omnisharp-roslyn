using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Intellisense
{
    internal static class CompletionItemExtensions
    {
        private static MethodInfo _getSymbolsAsync;

        static CompletionItemExtensions()
        {
            var symbolCompletionItemType = typeof(CompletionItem).GetTypeInfo().Assembly.GetType("Microsoft.CodeAnalysis.Completion.Providers.SymbolCompletionItem");
            _getSymbolsAsync = symbolCompletionItemType.GetMethod("GetSymbolsAsync", BindingFlags.Public | BindingFlags.Static);
        }

        public static async Task<IEnumerable<ISymbol>> GetCompletionSymbolsAsync(this CompletionItem completionItem, IEnumerable<ISymbol> recommendedSymbols, Document document)
        {
            // for SymbolCompletionProvider, use the logic of extracting information from recommended symbols
            if (completionItem.Properties.ContainsKey("Provider") && completionItem.Properties["Provider"] == "Microsoft.CodeAnalysis.CSharp.Completion.Providers.SymbolCompletionProvider")
            {
                return recommendedSymbols.Where(x => x.Name == completionItem.Properties["SymbolName"] && (int)x.Kind == int.Parse(completionItem.Properties["SymbolKind"])).Distinct();
            }

            // if the completion provider encoded symbols into Properties, we can return them
            if (completionItem.Properties.ContainsKey("Symbols"))
            {
                // the API to decode symbols is not public at the moment
                // http://source.roslyn.io/#Microsoft.CodeAnalysis.Features/Completion/Providers/SymbolCompletionItem.cs,93
                var decodedSymbolsTask = _getSymbolsAsync.InvokeStatic<Task<ImmutableArray<ISymbol>>>(new object[] { completionItem, document, default(CancellationToken) });
                if (decodedSymbolsTask != null)
                {
                    return await decodedSymbolsTask;
                }
            }

            return Enumerable.Empty<ISymbol>();
        }
    }
}
