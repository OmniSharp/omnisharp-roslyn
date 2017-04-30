using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;

namespace OmniSharp.Roslyn.CSharp.Services.Intellisense
{
    internal static class CompletionItemExtensions
    {
        public static async Task<IEnumerable<ISymbol>> GetCompletionSymbols(this CompletionItem completionItem, IEnumerable<ISymbol> recommendedSymbols, Document document)
        {
            // for SymbolCompletionProvider, use the logic of extracting information from recommended symbols
            if (completionItem.Properties.ContainsKey("Provider") && completionItem.Properties["Provider"] == "Microsoft.CodeAnalysis.CSharp.Completion.Providers.SymbolCompletionProvider")
            {
                var symbols = recommendedSymbols.Where(x => x.Name == completionItem.Properties["SymbolName"] && (int)x.Kind == int.Parse(completionItem.Properties["SymbolKind"])).Distinct();
                return symbols ?? Enumerable.Empty<ISymbol>();
            }

            // if the completion provider encoded symbols into Properties, we can return them
            if (completionItem.Properties.ContainsKey("Symbols"))
            {
                var symbolCompletionItemType = typeof(CompletionItem).GetTypeInfo().Assembly.GetType("Microsoft.CodeAnalysis.Completion.Providers.SymbolCompletionItem");
                var getSymbolsAsync = symbolCompletionItemType.GetMethod("GetSymbolsAsync", BindingFlags.Public | BindingFlags.Static);
                var decodedSymbolsTask = getSymbolsAsync.Invoke(null, new object[] { completionItem, document, default(CancellationToken) }) as Task<ImmutableArray<ISymbol>>;
                if (decodedSymbolsTask != null)
                {
                    return await decodedSymbolsTask;
                }
            }

            return Enumerable.Empty<ISymbol>();
        }
    }
}
