#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Completion;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using OmniSharp.Models;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Roslyn.CSharp.Helpers;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Utilities;
using CompletionItem = OmniSharp.Models.v1.Completion.CompletionItem;
using CSharpCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using CSharpCompletionList = Microsoft.CodeAnalysis.Completion.CompletionList;
using CSharpCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace OmniSharp.Roslyn.CSharp.Services.Completion
{
    internal static partial class CompletionListBuilder
    {
        internal static async Task<(IReadOnlyList<CompletionItem>, bool)> BuildCompletionItemsSync(
            Document document,
            SourceText sourceText,
            long cacheId,
            int position,
            CSharpCompletionService completionService,
            CSharpCompletionList completions,
            TextSpan typedSpan,
            bool expectingImportedItems,
            bool isSuggestionMode)
        {
            var completionsBuilder = new List<CompletionItem>(completions.ItemsList.Count);
            var seenUnimportedCompletions = false;
            var commitCharacterRuleCache = new Dictionary<ImmutableArray<CharacterSetModificationRule>, IReadOnlyList<char>>();
            var commitCharacterRuleBuilder = new HashSet<char>();

            var completionTasksAndProviderNamesBuilder = ImmutableArray.CreateBuilder<(Task<CompletionChange>?, string? providerName)>();
            for (int i = 0; i < completions.ItemsList.Count; i++)
            {
                var completion = completions.ItemsList[i];
                var providerName = completion.GetProviderName();
                if (providerName is TypeImportCompletionProvider or
                                    ExtensionMethodImportCompletionProvider)
                {
                    completionTasksAndProviderNamesBuilder.Add((null, providerName));
                }
                else
                {
                    completionTasksAndProviderNamesBuilder.Add(((Task<CompletionChange>?)completionService.GetChangeAsync(document, completion), providerName));
                }
            }
            var completionTasksAndProviderNames = completionTasksAndProviderNamesBuilder.ToImmutable();

            for (int i = 0; i < completions.ItemsList.Count; i++)
            {
                TextSpan changeSpan = typedSpan;
                var completion = completions.ItemsList[i];

                // To-do: Add support for snippet items: https://github.com/OmniSharp/omnisharp-roslyn/issues/2485
                if (completion.GetProviderName() == SnippetCompletionProvider)
                {
                    continue;
                }

                var insertTextFormat = InsertTextFormat.PlainText;
                string labelText = completion.DisplayTextPrefix + completion.DisplayText + completion.DisplayTextSuffix;
                List<LinePositionSpanTextChange>? additionalTextEdits = null;
                string? insertText = null;
                string? filterText = null;
                string? sortText = null;
                var (changeTask, providerName) = completionTasksAndProviderNames[i];
                switch (providerName)
                {
                    case TypeImportCompletionProvider or ExtensionMethodImportCompletionProvider:
                        changeSpan = typedSpan;
                        insertText = completion.DisplayText;
                        seenUnimportedCompletions = true;
                        sortText = '1' + completion.SortText;
                        filterText = null;
                        break;

                    case InternalsVisibleToCompletionProvider:
                        // if the completion is for the hidden Misc files project, skip it
                        if (completion.DisplayText == Configuration.OmniSharpMiscProjectName) continue;
                        goto default;

                    default:
                        {
                            // Except for import completion, we just resolve the change up front in the sync version. It's only expensive
                            // for override completion, but there's not a heck of a lot we can do about that for the sync scenario
                            Debug.Assert(changeTask is not null);

                            GetCompletionInfo(
                                sourceText,
                                position,
                                completion,
                                await changeTask!,
                                typedSpan,
                                labelText,
                                expectingImportedItems,
                                out insertText, out filterText, out sortText, out insertTextFormat, out changeSpan, out additionalTextEdits);

                            break;
                        }
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
                });
            }

            return (completionsBuilder, seenUnimportedCompletions);
        }

        private static void GetCompletionInfo(
            SourceText sourceText,
            int position,
            CSharpCompletionItem completion,
            CompletionChange change,
            TextSpan typedSpan,
            string labelText,
            bool expectingImportedItems,
            out string? insertText,
            out string? filterText,
            out string? sortText,
            out InsertTextFormat insertTextFormat,
            out TextSpan changeSpan,
            out List<LinePositionSpanTextChange>? additionalTextEdits)
        {
            insertTextFormat = InsertTextFormat.PlainText;
            changeSpan = typedSpan;
            insertText = null;
            filterText = null;
            sortText = null;
            additionalTextEdits = null;

            // Roslyn will give us the position to move the cursor after the completion is entered.
            // However, this is in the _new_ document, after changes have been applied. In order to
            // snippetize the insertion text, we need to calculate the offset as we move along the
            // edits, subtracting or adding the difference for edits that do not insersect the current
            // span.
            var adjustedNewPosition = change!.NewPosition;

            var cursorPoint = sourceText.GetPointFromPosition(position);
            var lineStartPosition = sourceText.GetPositionFromLineAndOffset(cursorPoint.Line, offset: 0);

            // There must be at least one change that affects the current location, or something is seriously wrong
            Debug.Assert(change.TextChanges.Any(change => change.Span.IntersectsWith(position)));

            foreach (var textChange in change.TextChanges)
            {
                if (!textChange.Span.IntersectsWith(position))
                {
                    handleNonInsertsectingEdit(sourceText, ref additionalTextEdits, ref adjustedNewPosition, textChange);
                }
                else
                {
                    // Either there should be no new position, or it should be within the text that is being added
                    // by this change.
                    int changeSpanStart = textChange.Span.Start;
                    Debug.Assert(adjustedNewPosition is null ||
                        (adjustedNewPosition.Value <= changeSpanStart + textChange.NewText!.Length) &&
                        (adjustedNewPosition.Value >= changeSpanStart));

                    // Filtering needs a range that is a _single_ line. Consider a case like this (whitespace documented with escapes):
                    //
                    // 1: class C
                    // 2: {\t\r\n
                    // 3:    override $$
                    //
                    // Roslyn will see the trailing \t on line 2 and remove it when creating the _main_ text change. However, that will
                    // break filtering because filtering expects a single line as part of the range. So what we want to do is break the
                    // the text change up into two: one to cover the previous line, as an additional edit, and then one to cover the
                    // rest of the change.

                    var updatedChange = textChange;

                    if (changeSpanStart < lineStartPosition)
                    {
                        // We know we're in the special case. In order to correctly determine the amount of leading newlines to trim, we want
                        // to calculate the number of lines before the cursor we're editing
                        var editStartPoint = sourceText.GetPointFromPosition(changeSpanStart);
                        var numLinesEdited = cursorPoint.Line - editStartPoint.Line;

                        Debug.Assert(textChange.NewText != null);

                        // Now count that many newlines forward in the edited text
                        int cutoffPosition = 0;
                        for (int numNewlinesFound = 0; numNewlinesFound < numLinesEdited; cutoffPosition++)
                        {
                            if (textChange.NewText![cutoffPosition] == '\n')
                            {
                                numNewlinesFound++;
                            }
                        }

                        // Now that we've found the cuttoff, we can build our two subchanges
                        var prefixChange = new TextChange(new TextSpan(changeSpanStart, length: lineStartPosition - changeSpanStart), textChange.NewText!.Substring(0, cutoffPosition));
                        handleNonInsertsectingEdit(sourceText, ref additionalTextEdits, ref adjustedNewPosition, prefixChange);
                        updatedChange = new TextChange(new TextSpan(lineStartPosition, length: textChange.Span.End - lineStartPosition), textChange.NewText.Substring(cutoffPosition));
                    }

                    changeSpan = updatedChange.Span;
                    // When inserting at the beginning or middle of a word, we want to only replace characters
                    // up until the caret position, but not after.  For example when typing at the beginning of a word
                    // we only want to insert the completion before the rest of the word.
                    // However, Roslyn returns the entire word as the span to replace, so we have to adjust it.
                    if (position < changeSpan.End)
                    {
                        changeSpan = new(changeSpan.Start, length: position - changeSpan.Start);
                    }

                    (insertText, insertTextFormat) = getPossiblySnippetizedInsertText(updatedChange, adjustedNewPosition);

                    // If we're expecting there to be unimported types, put in an explicit sort text to put things already in scope first.
                    // Otherwise, omit the sort text if it's the same as the label to save on space.
                    sortText = GetSortText(completion, labelText, expectingImportedItems);

                    // If the completion is replacing a bigger range than the previously-typed word, we need to have the filter
                    // text compensate. Clients will use the range of the text edit to determine the thing that is being filtered
                    // against. For example, override completion:
                    //
                    //    override $$
                    //    |--------| Range that is being changed by the completion
                    //
                    // That means vscode will consider "override <additional user input>" when looking to see whether the item
                    // still matches. To compensate, we add the start of the replacing range, up to the start of the current word,
                    // to ensure the item isn't silently filtered out.

                    if (changeSpan != typedSpan)
                    {
                        if (typedSpan.Start < changeSpan.Start)
                        {
                            // This means that some part of the currently-typed text is an exact match for the start of the
                            // change, so chop off changeSpan.Start - typedSpan.Start from the filter text to get it to match
                            // up with the range
                            int prefixMatchElement = changeSpan.Start - typedSpan.Start;
                            Debug.Assert(completion.FilterText!.StartsWith(sourceText.GetSubText(new TextSpan(typedSpan.Start, prefixMatchElement)).ToString()));
                            filterText = completion.FilterText.Substring(prefixMatchElement);
                        }
                        else
                        {
                            var prefix = sourceText.GetSubText(TextSpan.FromBounds(changeSpan.Start, typedSpan.Start)).ToString();
                            filterText = prefix + completion.FilterText;
                        }
                    }
                    else
                    {
                        filterText = labelText == completion.FilterText ? null : completion.FilterText;
                    }
                }
            }

            static (string?, InsertTextFormat) getPossiblySnippetizedInsertText(TextChange change, int? adjustedNewPosition)
            {
                if (adjustedNewPosition is not int newPosition || change.NewText is null || newPosition == change.Span.Start + change.NewText.Length)
                {
                    return (change.NewText, InsertTextFormat.PlainText);
                }

                // Roslyn wants to move the cursor somewhere inside the result. Substring from the
                // requested start to the new position, and from the new position to the end of the
                // string.
                int midpoint = newPosition - change.Span.Start;
                var beforeText = LspSnippetHelpers.Escape(change.NewText.Substring(0, midpoint));
                var afterText = LspSnippetHelpers.Escape(change.NewText.Substring(midpoint));

                return ($"{beforeText}$0{afterText}", InsertTextFormat.Snippet);
            }

            static void handleNonInsertsectingEdit(SourceText sourceText, ref List<LinePositionSpanTextChange>? additionalTextEdits, ref int? adjustedNewPosition, TextChange textChange)
            {
                additionalTextEdits ??= new();
                additionalTextEdits.Add(TextChanges.Convert(sourceText, textChange));

                if (adjustedNewPosition is int newPosition)
                {
                    // Find the diff between the original text length and the new text length.
                    var diff = (textChange.NewText?.Length ?? 0) - textChange.Span.Length;

                    // If the new text is longer than the replaced text, we want to subtract that
                    // length from the current new position to find the adjusted position in the old
                    // document. If the new text was shorter, diff will be negative, and subtracting
                    // will result in increasing the adjusted position as expected
                    adjustedNewPosition = newPosition - diff;
                }
            }
        }

        private static string? GetSortText(CSharpCompletionItem completion, string labelText, bool expectingImportedItems)
        {
            return expectingImportedItems
                ? '0' + completion.SortText
                : labelText == completion.SortText ? null : completion.SortText;
        }
    }
}
