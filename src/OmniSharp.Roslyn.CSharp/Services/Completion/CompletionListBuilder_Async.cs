#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Completion;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Models.v1.Completion;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
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
            var completionsBuilder = new List<CompletionItem>(completions.Items.Length);
            var seenUnimportedCompletions = false;
            var commitCharacterRuleCache = new Dictionary<ImmutableArray<CharacterSetModificationRule>, IReadOnlyList<char>>();
            var commitCharacterRuleBuilder = new HashSet<char>();
            var isOverrideOrPartialCompletion = completions.Items.Length > 0
                && completions.Items[0].GetProviderName() is OverrideCompletionProvider or PartialMethodCompletionProvider;

            for (int i = 0; i < completions.Items.Length; i++)
            {
                var completion = completions.Items[i];
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
                    // The completion is somehow expensive. Currently, this one of two categories: import completion, or override/partial
                    // completion.
                    Debug.Assert(completion.GetProviderName() is OverrideCompletionProvider or PartialMethodCompletionProvider
                                                              or TypeImportCompletionProvider or ExtensionMethodImportCompletionProvider);

                    changeSpan = typedSpan;

                    if (isOverrideOrPartialCompletion)
                    {
                        // For override and partial completion, we don't want to use the DisplayText as the insert text because they contain
                        // characters that will affect our ability to asynchronously resolve the change later.
                        insertText = completion.FilterText;
                        sortText = GetSortText(completion, labelText, expectingImportedItems);
                        hasAfterInsertStep = true;
                    }
                    else
                    {
                        insertText = completion.DisplayText;
                        sortText = '1' + completion.SortText;
                        seenUnimportedCompletions = true;
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

                var commitCharacters = BuildCommitCharacters(completion.Rules.CommitCharacterRules, isSuggestionMode, commitCharacterRuleCache, commitCharacterRuleBuilder);

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
                    Preselect = completion.Rules.SelectionBehavior == CompletionItemSelectionBehavior.HardSelection,
                    CommitCharacters = commitCharacters,
                    HasAfterInsertStep = hasAfterInsertStep,
                });
            }

            return (completionsBuilder, seenUnimportedCompletions);
        }
    }
}
