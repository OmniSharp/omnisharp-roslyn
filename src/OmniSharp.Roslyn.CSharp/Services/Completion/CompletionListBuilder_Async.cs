#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Completion;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Models.v1.Completion;
using CompletionItem = OmniSharp.Models.v1.Completion.CompletionItem;
using CSharpCompletionList = Microsoft.CodeAnalysis.Completion.CompletionList;
using CSharpCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace OmniSharp.Roslyn.CSharp.Services.Completion
{
    internal static partial class CompletionListBuilder
    {
        internal static async Task<(IReadOnlyList<CompletionItem>, bool)> BuildCompletionItemsAsync(
            Document document,
            SourceText sourceText,
            long cacheId,
            int position,
            CSharpCompletionService completionService,
            CSharpCompletionList completions,
            TextSpan typedSpan,
            bool expectingImportedItems, bool isSuggestionMode)
        {
            var completionsBuilder = new List<CompletionItem>(completions.ItemsList.Count);
            var seenUnimportedCompletions = false;
            var commitCharacterRuleCache = new Dictionary<ImmutableArray<CharacterSetModificationRule>, IReadOnlyList<char>>();
            var commitCharacterRuleBuilder = new HashSet<char>();

            for (int i = 0; i < completions.ItemsList.Count; i++)
            {
                var completion = completions.ItemsList[i];
                string labelText = completion.DisplayTextPrefix + completion.DisplayText + completion.DisplayTextSuffix;
                string? insertText;
                string? filterText = null;
                List<LinePositionSpanTextChange>? additionalTextEdits = null;
                InsertTextFormat insertTextFormat = InsertTextFormat.PlainText;
                TextSpan changeSpan;
                string? sortText;
                bool hasAfterInsertStep = false;
                if (completion.IsComplexTextEdit)
                {
                    // To-do: Add support for snippet items: https://github.com/OmniSharp/omnisharp-roslyn/issues/2485
                    if (completion.GetProviderName() == SnippetCompletionProvider)
                    {
                        continue;
                    }

                    // The completion is somehow expensive. Currently, this one of two categories: import completion or override/partial completion.
                    Debug.Assert(completion.GetProviderName() is OverrideCompletionProvider or PartialMethodCompletionProvider
                                                              or TypeImportCompletionProvider or ExtensionMethodImportCompletionProvider
                                                              or AwaitCompletionProvider);

                    changeSpan = typedSpan;

                    switch (completion.GetProviderName())
                    {
                        case OverrideCompletionProvider or PartialMethodCompletionProvider or AwaitCompletionProvider:
                            // For override and partial completion, we don't want to use the DisplayText as the insert text because they contain
                            // characters that will affect our ability to asynchronously resolve the change later.
                            insertText = completion.FilterText;
                            sortText = GetSortText(completion, labelText, expectingImportedItems);
                            hasAfterInsertStep = true;
                            break;

                        default: // case TypeImportCompletionProvider or ExtensionMethodImportCompletionProvider:
                            insertText = completion.DisplayText;
                            sortText = '1' + completion.SortText;
                            seenUnimportedCompletions = true;
                            break;
                    }
                }
                else
                {
                    // For non-complex completions, just await the text edit. It's cheap enough that it doesn't impact our ability
                    // to pop completions quickly

                    // If the completion item is the misc project name, skip it.
                    if (completion.DisplayText == Configuration.OmniSharpMiscProjectName) continue;

                    GetCompletionInfo(
                        sourceText,
                        position,
                        completion,
                        await completionService.GetChangeAsync(document, completion),
                        typedSpan,
                        labelText,
                        expectingImportedItems,
                        out insertText, out filterText, out sortText, out insertTextFormat, out changeSpan, out additionalTextEdits);
                }

                var treatAsASuggestion = isSuggestionMode || ShouldTreatCompletionItemAsSuggestion(completion, typedSpan);
                var commitCharacters = BuildCommitCharacters(completion.Rules.CommitCharacterRules, treatAsASuggestion, commitCharacterRuleCache, commitCharacterRuleBuilder);

                completionsBuilder.Add(new CompletionItem
                {
                    Label = labelText,
                    TextEdit = GetChangeForTextAndSpan(insertText!, changeSpan, sourceText),
                    InsertTextFormat = insertTextFormat,
                    AdditionalTextEdits = additionalTextEdits,
                    SortText = sortText,
                    FilterText = filterText,
                    Kind = GetCompletionItemKind(completion.Tags),
                    Detail = completion.InlineDescription,
                    Data = (cacheId, i),
                    Preselect = completion.Rules.MatchPriority == MatchPriority.Preselect,
                    CommitCharacters = commitCharacters,
                    HasAfterInsertStep = hasAfterInsertStep,
                });
            }

            return (completionsBuilder, seenUnimportedCompletions);
        }
    }
}
