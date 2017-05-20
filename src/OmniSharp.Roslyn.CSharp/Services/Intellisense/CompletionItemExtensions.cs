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
        private const string GetSymbolsAsync = nameof(GetSymbolsAsync);
        private const string InsertionText = nameof(InsertionText);
        private const string NamedParameterCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.NamedParameterCompletionProvider";
        private const string OverrideCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.OverrideCompletionProvider";
        private const string ParitalMethodCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.PartialMethodCompletionProvider";
        private const string Provider = nameof(Provider);
        private const string SymbolCompletionItem = "Microsoft.CodeAnalysis.Completion.Providers.SymbolCompletionItem";
        private const string SymbolCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.SymbolCompletionProvider";
        private const string SymbolKind = nameof(SymbolKind);
        private const string SymbolName = nameof(SymbolName);
        private const string Symbols = nameof(Symbols);

        private static MethodInfo _getSymbolsAsync;

        static CompletionItemExtensions()
        {
            var symbolCompletionItemType = typeof(CompletionItem).GetTypeInfo().Assembly.GetType(SymbolCompletionItem);
            _getSymbolsAsync = symbolCompletionItemType.GetMethod(GetSymbolsAsync, BindingFlags.Public | BindingFlags.Static);
        }

        public static async Task<IEnumerable<ISymbol>> GetCompletionSymbolsAsync(this CompletionItem completionItem, IEnumerable<ISymbol> recommendedSymbols, Document document)
        {
            var properties = completionItem.Properties;

            // for SymbolCompletionProvider, use the logic of extracting information from recommended symbols
            if (properties.TryGetValue(Provider, out var provider) && provider == SymbolCompletionProvider)
            {
                return recommendedSymbols.Where(x => x.Name == properties[SymbolName] && (int)x.Kind == int.Parse(properties[SymbolKind])).Distinct();
            }

            // if the completion provider encoded symbols into Properties, we can return them
            if (properties.ContainsKey(Symbols))
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

        public static bool UseDisplayTextAsCompletionText(this CompletionItem completionItem)
        {
            return completionItem.Properties.TryGetValue(Provider, out var provider)
                && (provider == NamedParameterCompletionProvider || provider == OverrideCompletionProvider || provider == ParitalMethodCompletionProvider);
        }

        public static bool TryGetInsertionText(this CompletionItem completionItem, out string insertionText)
        {
            return completionItem.Properties.TryGetValue(InsertionText, out insertionText);
        }
    }
}
