using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Completion;
using Microsoft.CodeAnalysis.Options;
using OmniSharp.Models.AutoComplete;
using OmniSharp.Roslyn.CSharp.Services.Completion;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Intellisense
{
    internal static class CompletionItemExtensions
    {
        private const string GetSymbolsAsync = nameof(GetSymbolsAsync);
        private const string InsertionText = nameof(InsertionText);
        private const string SymbolCompletionItem = "Microsoft.CodeAnalysis.Completion.Providers.SymbolCompletionItem";
        private const string NamedParameterCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.NamedParameterCompletionProvider";
        private const string SymbolKind = nameof(SymbolKind);
        private const string SymbolName = nameof(SymbolName);
        private const string Symbols = nameof(Symbols);
        private static readonly Type _symbolCompletionItemType;
        private static readonly MethodInfo _getSymbolsAsync;
        internal static readonly PerLanguageOption<bool?> ShowItemsFromUnimportedNamespaces = new PerLanguageOption<bool?>("CompletionOptions", "ShowItemsFromUnimportedNamespaces", defaultValue: null);

        static CompletionItemExtensions()
        {
            _symbolCompletionItemType = typeof(CompletionItem).GetTypeInfo().Assembly.GetType(SymbolCompletionItem);
            _getSymbolsAsync = _symbolCompletionItemType.GetMethod(GetSymbolsAsync, BindingFlags.Public | BindingFlags.Static);
        }

        public static async Task<IEnumerable<ISymbol>> GetCompletionSymbolsAsync(this CompletionItem completionItem, IEnumerable<ISymbol> recommendedSymbols, Document document)
        {
            var properties = completionItem.Properties;

            if (completionItem.GetType() == _symbolCompletionItemType || properties.ContainsKey(Symbols))
            {
                var decodedSymbolsTask = _getSymbolsAsync.InvokeStatic<Task<ImmutableArray<ISymbol>>>(new object[] { completionItem, document, default(CancellationToken) });
                if (decodedSymbolsTask != null)
                {
                    return await decodedSymbolsTask;
                }
            }

            // if the completion provider encoded symbols into Properties, we can return them
            if (properties.TryGetValue(SymbolName, out string symbolNameValue)
                && properties.TryGetValue(SymbolKind, out string symbolKindValue)
                && int.Parse(symbolKindValue) is int symbolKindInt)
            {
#pragma warning disable RS1024 // Compare symbols correctly: service is deprecated, not going to change behavior now.
                return recommendedSymbols
                    .Where(x => (int)x.Kind == symbolKindInt && x.Name.Equals(symbolNameValue, StringComparison.OrdinalIgnoreCase))
                    .Distinct();
#pragma warning restore RS1024 // Compare symbols correctly
            }

            return Enumerable.Empty<ISymbol>();
        }

        public static bool UseDisplayTextAsCompletionText(this CompletionItem completionItem)
            => completionItem.GetProviderName() is NamedParameterCompletionProvider
                                                or CompletionListBuilder.OverrideCompletionProvider
                                                or CompletionListBuilder.PartialMethodCompletionProvider;

        public static bool TryGetInsertionText(this CompletionItem completionItem, out string insertionText) => completionItem.Properties.TryGetValue(InsertionText, out insertionText);

        public static AutoCompleteResponse ToAutoCompleteResponse(this CompletionItem item, bool wantKind, bool isSuggestionMode, bool preselect)
        {
            // for simple use cases we'll just assume that the completion text is the same as the display text
            var response = new AutoCompleteResponse()
            {
                CompletionText = item.DisplayText,
                DisplayText = item.DisplayText,
                Snippet = item.DisplayText,
                Kind = wantKind ? item.Tags.FirstOrDefault() : null,
                IsSuggestionMode = isSuggestionMode,
                Preselect = preselect
            };

            // if provider name is "Microsoft.CodeAnalysis.CSharp.Completion.Providers.EmbeddedLanguageCompletionProvider"
            // we have access to more elaborate description
            if (item.GetProviderName() == CompletionListBuilder.EmeddedLanguageCompletionProvider)
            {
                response.DisplayText = item.InlineDescription;
                if (item.Properties.TryGetValue("DescriptionKey", out var description))
                {
                    response.Description = description;
                }
            }

            return response;
        }
    }
}
