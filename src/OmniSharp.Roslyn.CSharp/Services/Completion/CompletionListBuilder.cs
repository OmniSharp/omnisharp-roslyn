#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Models.v1.Completion;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using CompletionItem = OmniSharp.Models.v1.Completion.CompletionItem;
using CSharpCompletionList = Microsoft.CodeAnalysis.Completion.CompletionList;
using CSharpCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace OmniSharp.Roslyn.CSharp.Services.Completion
{
    internal static partial class CompletionListBuilder
    {
        private static readonly Dictionary<string, CompletionItemKind> s_roslynTagToCompletionItemKind = new()
        {
            { WellKnownTags.Public, CompletionItemKind.Keyword },
            { WellKnownTags.Protected, CompletionItemKind.Keyword },
            { WellKnownTags.Private, CompletionItemKind.Keyword },
            { WellKnownTags.Internal, CompletionItemKind.Keyword },
            { WellKnownTags.File, CompletionItemKind.File },
            { WellKnownTags.Project, CompletionItemKind.File },
            { WellKnownTags.Folder, CompletionItemKind.Folder },
            { WellKnownTags.Assembly, CompletionItemKind.File },
            { WellKnownTags.Class, CompletionItemKind.Class },
            { WellKnownTags.Constant, CompletionItemKind.Constant },
            { WellKnownTags.Delegate, CompletionItemKind.Function },
            { WellKnownTags.Enum, CompletionItemKind.Enum },
            { WellKnownTags.EnumMember, CompletionItemKind.EnumMember },
            { WellKnownTags.Event, CompletionItemKind.Event },
            { WellKnownTags.ExtensionMethod, CompletionItemKind.Method },
            { WellKnownTags.Field, CompletionItemKind.Field },
            { WellKnownTags.Interface, CompletionItemKind.Interface },
            { WellKnownTags.Intrinsic, CompletionItemKind.Text },
            { WellKnownTags.Keyword, CompletionItemKind.Keyword },
            { WellKnownTags.Label, CompletionItemKind.Text },
            { WellKnownTags.Local, CompletionItemKind.Variable },
            { WellKnownTags.Namespace, CompletionItemKind.Module },
            { WellKnownTags.Method, CompletionItemKind.Method },
            { WellKnownTags.Module, CompletionItemKind.Module },
            { WellKnownTags.Operator, CompletionItemKind.Operator },
            { WellKnownTags.Parameter, CompletionItemKind.Variable },
            { WellKnownTags.Property, CompletionItemKind.Property },
            { WellKnownTags.RangeVariable, CompletionItemKind.Variable },
            { WellKnownTags.Reference, CompletionItemKind.Reference },
            { WellKnownTags.Structure, CompletionItemKind.Struct },
            { WellKnownTags.TypeParameter, CompletionItemKind.TypeParameter },
            { WellKnownTags.Snippet, CompletionItemKind.Snippet },
            { WellKnownTags.Error, CompletionItemKind.Text },
            { WellKnownTags.Warning, CompletionItemKind.Text },
        };

        internal const string ObjectCreationCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.ObjectCreationCompletionProvider";
        internal const string OverrideCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.OverrideCompletionProvider";
        internal const string PartialMethodCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.PartialMethodCompletionProvider";
        internal const string InternalsVisibleToCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.InternalsVisibleToCompletionProvider";
        internal const string XmlDocCommentCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.XmlDocCommentCompletionProvider";
        internal const string TypeImportCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.TypeImportCompletionProvider";
        internal const string ExtensionMethodImportCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.ExtensionMethodImportCompletionProvider";
        internal const string EmeddedLanguageCompletionProvider = "Microsoft.CodeAnalysis.CSharp.Completion.Providers.EmbeddedLanguageCompletionProvider";

        // VS has a more complex concept of a commit mode vs suggestion mode for intellisense.
        // LSP doesn't have this, so mock it as best we can by removing space ` ` from the list
        // of commit characters if we're in suggestion mode.
        private static readonly IReadOnlyList<char> DefaultRulesWithoutSpace = CompletionRules.Default.DefaultCommitCharacters.Where(c => c != ' ').ToList();

        internal static async Task<(IReadOnlyList<CompletionItem>, bool)> BuildCompletionItems(
            Document document,
            SourceText sourceText,
            long cacheId,
            int position,
            CSharpCompletionService completionService,
            CSharpCompletionList completions,
            TextSpan typedSpan,
            bool expectingImportedItems,
            bool isSuggestionMode, bool enableAsyncCompletion)
            => enableAsyncCompletion
                ? await BuildCompletionItemsAsync(document, sourceText, cacheId, position, completionService, completions, typedSpan, expectingImportedItems, isSuggestionMode)
                : await BuildCompletionItemsSync(document, sourceText, cacheId, position, completionService, completions, typedSpan, expectingImportedItems, isSuggestionMode);

        internal static LinePositionSpanTextChange GetChangeForTextAndSpan(string? insertText, TextSpan changeSpan, SourceText sourceText)
        {
            var changeLinePositionSpan = sourceText.Lines.GetLinePositionSpan(changeSpan);
            return new()
            {
                NewText = insertText ?? "",
                StartLine = changeLinePositionSpan.Start.Line,
                StartColumn = changeLinePositionSpan.Start.Character,
                EndLine = changeLinePositionSpan.End.Line,
                EndColumn = changeLinePositionSpan.End.Character
            };
        }

        private static IReadOnlyList<char>? BuildCommitCharacters(ImmutableArray<CharacterSetModificationRule> characterRules, bool isSuggestionMode, Dictionary<ImmutableArray<CharacterSetModificationRule>, IReadOnlyList<char>> commitCharacterRulesCache, HashSet<char> commitCharactersBuilder)
        {
            if (characterRules.IsEmpty)
            {
                // Use defaults
                return isSuggestionMode ? DefaultRulesWithoutSpace : CompletionRules.Default.DefaultCommitCharacters;
            }

            if (commitCharacterRulesCache.TryGetValue(characterRules, out var cachedRules))
            {
                return cachedRules;
            }

            addAllCharacters(CompletionRules.Default.DefaultCommitCharacters);

            foreach (var modifiedRule in characterRules)
            {
                switch (modifiedRule.Kind)
                {
                    case CharacterSetModificationKind.Add:
                        commitCharactersBuilder.UnionWith(modifiedRule.Characters);
                        break;

                    case CharacterSetModificationKind.Remove:
                        commitCharactersBuilder.ExceptWith(modifiedRule.Characters);
                        break;

                    case CharacterSetModificationKind.Replace:
                        commitCharactersBuilder.Clear();
                        addAllCharacters(modifiedRule.Characters);
                        break;
                }
            }

            // VS has a more complex concept of a commit mode vs suggestion mode for intellisense.
            // LSP doesn't have this, so mock it as best we can by removing space ` ` from the list
            // of commit characters if we're in suggestion mode.
            if (isSuggestionMode)
            {
                commitCharactersBuilder.Remove(' ');
            }

            var finalCharacters = commitCharactersBuilder.ToList();
            commitCharactersBuilder.Clear();

            commitCharacterRulesCache.Add(characterRules, finalCharacters);

            return finalCharacters;

            void addAllCharacters(ImmutableArray<char> characters)
            {
                foreach (var @char in characters)
                {
                    commitCharactersBuilder.Add(@char);
                }
            }
        }

        private static CompletionItemKind GetCompletionItemKind(ImmutableArray<string> tags)
        {
            foreach (var tag in tags)
            {
                if (s_roslynTagToCompletionItemKind.TryGetValue(tag, out var itemKind))
                {
                    return itemKind;
                }
            }

            return CompletionItemKind.Text;
        }
    }
}
