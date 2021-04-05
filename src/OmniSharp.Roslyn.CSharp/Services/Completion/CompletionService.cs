#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Helpers;
using OmniSharp.Roslyn.CSharp.Services.Intellisense;
using OmniSharp.Utilities;
using CompletionItem = OmniSharp.Models.v1.Completion.CompletionItem;
using CompletionTriggerKind = OmniSharp.Models.v1.Completion.CompletionTriggerKind;
using CSharpCompletionList = Microsoft.CodeAnalysis.Completion.CompletionList;
using CSharpCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace OmniSharp.Roslyn.CSharp.Services.Completion
{
    [Shared]
    [OmniSharpHandler(OmniSharpEndpoints.Completion, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.CompletionResolve, LanguageNames.CSharp)]
    public class CompletionService :
        IRequestHandler<CompletionRequest, CompletionResponse>,
        IRequestHandler<CompletionResolveRequest, CompletionResolveResponse>
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

        // VS has a more complex concept of a commit mode vs suggestion mode for intellisense.
        // LSP doesn't have this, so mock it as best we can by removing space ` ` from the list
        // of commit characters if we're in suggestion mode.
        private static readonly IReadOnlyList<char> DefaultRulesWithoutSpace = CompletionRules.Default.DefaultCommitCharacters.Where(c => c != ' ').ToList();

        private readonly OmniSharpWorkspace _workspace;
        private readonly FormattingOptions _formattingOptions;
        private readonly ILogger _logger;

        private readonly object _lock = new();
        private (CSharpCompletionList Completions, string FileName, int position)? _lastCompletion = null;

        [ImportingConstructor]
        public CompletionService(OmniSharpWorkspace workspace, FormattingOptions formattingOptions, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _formattingOptions = formattingOptions;
            _logger = loggerFactory.CreateLogger<CompletionService>();
        }

        public async Task<CompletionResponse> Handle(CompletionRequest request)
        {
            _logger.LogTrace("Completions requested");
            lock (_lock)
            {
                _lastCompletion = null;
            }

            var document = _workspace.GetDocument(request.FileName);
            if (document is null)
            {
                _logger.LogInformation("Could not find document for file {0}", request.FileName);
                return new CompletionResponse { Items = ImmutableArray<CompletionItem>.Empty };
            }

            var sourceText = await document.GetTextAsync();
            var position = sourceText.GetTextPosition(request);

            var completionService = CSharpCompletionService.GetService(document);
            Debug.Assert(request.TriggerCharacter != null || request.CompletionTrigger != CompletionTriggerKind.TriggerCharacter);

            CompletionTrigger trigger = request.CompletionTrigger switch
            {
                CompletionTriggerKind.Invoked => CompletionTrigger.Invoke,
                CompletionTriggerKind.TriggerCharacter when request.TriggerCharacter is char c => CompletionTrigger.CreateInsertionTrigger(c),
                _ => CompletionTrigger.Invoke,
            };

            if (request.CompletionTrigger == CompletionTriggerKind.TriggerCharacter &&
                !completionService.ShouldTriggerCompletion(sourceText, position, trigger))
            {
                _logger.LogTrace("Should not insert completions here.");
                return new CompletionResponse { Items = ImmutableArray<CompletionItem>.Empty };
            }

            var (completions, expandedItemsAvailable) = await completionService.GetCompletionsInternalAsync(document, position, trigger);
            _logger.LogTrace("Found {0} completions for {1}:{2},{3}",
                             completions?.Items.IsDefaultOrEmpty != false ? 0 : completions.Items.Length,
                             request.FileName,
                             request.Line,
                             request.Column);

            if (completions is null || completions.Items.Length == 0)
            {
                return new CompletionResponse { Items = ImmutableArray<CompletionItem>.Empty };
            }

            if (request.TriggerCharacter == ' ' && !completions.Items.Any(c =>
            {
                var providerName = c.GetProviderName();
                return providerName is CompletionItemExtensions.OverrideCompletionProvider or
                                       CompletionItemExtensions.PartialMethodCompletionProvider or
                                       CompletionItemExtensions.ObjectCreationCompletionProvider;
            }))
            {
                // Only trigger on space if there is an object creation completion
                return new CompletionResponse { Items = ImmutableArray<CompletionItem>.Empty };
            }

            var typedSpan = completionService.GetDefaultCompletionListSpan(sourceText, position);
            string typedText = sourceText.GetSubText(typedSpan).ToString();
            _logger.LogTrace("Completions filled in");

            lock (_lock)
            {
                _lastCompletion = (completions, request.FileName, position);
            }

            var commitCharacterRuleBuilder = new HashSet<char>();
            var commitCharacterRuleCache = new Dictionary<ImmutableArray<CharacterSetModificationRule>, IReadOnlyList<char>>();
            var completionsBuilder = ImmutableArray.CreateBuilder<CompletionItem>(completions.Items.Length);

            // If we don't encounter any unimported types, and the completion context thinks that some would be available, then
            // that completion provider is still creating the cache. We'll mark this completion list as not completed, and the
            // editor will ask again when the user types more. By then, hopefully the cache will have populated and we can mark
            // the completion as done.
            bool seenUnimportedCompletions = false;
            bool expectingImportedItems = expandedItemsAvailable && _workspace.Options.GetOption(CompletionItemExtensions.ShowItemsFromUnimportedNamespaces, LanguageNames.CSharp) == true;
            var syntax = await document.GetSyntaxTreeAsync();

            var replacingSpanStartPosition = sourceText.Lines.GetLinePosition(typedSpan.Start);
            var replacingSpanEndPosition = sourceText.Lines.GetLinePosition(typedSpan.End);
            var isSuggestionMode = completions.SuggestionModeItem is not null;

            var completionTasksAndProviderNames = completions.Items.SelectAsArray((document, completionService), (completion, arg) =>
            {
                var providerName = completion.GetProviderName();
                if (providerName is CompletionItemExtensions.TypeImportCompletionProvider or
                                    CompletionItemExtensions.ExtensionMethodImportCompletionProvider)
                {
                    return (null, providerName);
                }
                else
                {
                    return ((Task<CompletionChange>?)arg.completionService.GetChangeAsync(arg.document, completion), providerName);
                }
            });

            for (int i = 0; i < completions.Items.Length; i++)
            {
                TextSpan changeSpan = typedSpan;
                var completion = completions.Items[i];
                var insertTextFormat = InsertTextFormat.PlainText;
                string labelText = completion.DisplayTextPrefix + completion.DisplayText + completion.DisplayTextSuffix;
                List<LinePositionSpanTextChange>? additionalTextEdits = null;
                string? insertText = null;
                string? filterText = null;
                string? sortText = null;
                var (changeTask, providerName) = completionTasksAndProviderNames[i];
                switch (providerName)
                {
                    case CompletionItemExtensions.TypeImportCompletionProvider or CompletionItemExtensions.ExtensionMethodImportCompletionProvider:
                        changeSpan = typedSpan;
                        insertText = completion.DisplayText;
                        seenUnimportedCompletions = true;
                        sortText = '1' + completion.SortText;
                        filterText = null;
                        break;

                    case CompletionItemExtensions.InternalsVisibleToCompletionProvider:
                        // if the completion is for the hidden Misc files project, skip it
                        if (completion.DisplayText == Configuration.OmniSharpMiscProjectName) continue;
                        goto default;

                    default:
                        {
                            // Except for import completion, we just resolve the change up front in the sync version. It's only expensive
                            // for override completion, but there's not a heck of a lot we can do about that for the sync scenario
                            Debug.Assert(changeTask is not null);
                            var change = await changeTask!;

                            // Roslyn will give us the position to move the cursor after the completion is entered.
                            // However, this is in the _new_ document, after changes have been applied. In order to
                            // snippetize the insertion text, we need to calculate the offset as we move along the
                            // edits, subtracting or adding the difference for edits that do not insersect the current
                            // span.
                            var adjustedNewPosition = change!.NewPosition;

                            // There must be at least one change that affects the current location, or something is seriously wrong
                            Debug.Assert(change.TextChanges.Any(change => change.Span.IntersectsWith(position)));

                            foreach (var textChange in change.TextChanges)
                            {
                                if (!textChange.Span.IntersectsWith(position))
                                {
                                    additionalTextEdits ??= new();
                                    additionalTextEdits.Add(GetChangeForTextAndSpan(textChange.NewText!, textChange.Span, sourceText));

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
                                else
                                {
                                    // Either there should be no new position, or it should be within the text that is being added
                                    // by this change.
                                    Debug.Assert(adjustedNewPosition is null ||
                                        (adjustedNewPosition.Value <= textChange.Span.Start + textChange.NewText!.Length) &&
                                        (adjustedNewPosition.Value >= textChange.Span.Start));

                                    changeSpan = textChange.Span;
                                    (insertText, insertTextFormat) = getPossiblySnippitizedInsertText(textChange, adjustedNewPosition);

                                    // If we're expecting there to be unimported types, put in an explicit sort text to put things already in scope first.
                                    // Otherwise, omit the sort text if it's the same as the label to save on space.
                                    sortText = expectingImportedItems
                                        ? '0' + completion.SortText
                                        : labelText == completion.SortText ? null : completion.SortText;

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
                                            Debug.Assert(completion.FilterText.StartsWith(sourceText.GetSubText(new TextSpan(typedSpan.Start, prefixMatchElement)).ToString()));
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

                            break;
                        }
                }

                var commitCharacters = buildCommitCharacters(completion.Rules.CommitCharacterRules, isSuggestionMode, commitCharacterRuleCache, commitCharacterRuleBuilder);

                completionsBuilder.Add(new CompletionItem
                {
                    Label = labelText,
                    TextEdit = GetChangeForTextAndSpan(insertText!, changeSpan, sourceText),
                    InsertTextFormat = insertTextFormat,
                    AdditionalTextEdits = additionalTextEdits,
                    SortText = sortText,
                    FilterText = filterText,
                    Kind = getCompletionItemKind(completion.Tags),
                    Detail = completion.InlineDescription,
                    Data = i,
                    Preselect = completion.Rules.SelectionBehavior == CompletionItemSelectionBehavior.HardSelection,
                    CommitCharacters = commitCharacters,
                });
            }

            return new CompletionResponse
            {
                IsIncomplete = !seenUnimportedCompletions && expectingImportedItems,
                Items = completionsBuilder.Capacity == completionsBuilder.Count ? completionsBuilder.MoveToImmutable() : completionsBuilder.ToImmutable()
            };

            static (string?, InsertTextFormat) getPossiblySnippitizedInsertText(TextChange change, int? adjustedNewPosition)
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

            static CompletionItemKind getCompletionItemKind(ImmutableArray<string> tags)
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

            static IReadOnlyList<char>? buildCommitCharacters(
                ImmutableArray<CharacterSetModificationRule> characterRules,
                bool isSuggestionMode,
                Dictionary<ImmutableArray<CharacterSetModificationRule>, IReadOnlyList<char>> commitCharacterRulesCache,
                HashSet<char> commitCharactersBuilder)
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
        }

        public async Task<CompletionResolveResponse> Handle(CompletionResolveRequest request)
        {
            if (_lastCompletion is null)
            {
                _logger.LogError("Cannot call completion/resolve before calling completion!");
                return new CompletionResolveResponse { Item = request.Item };
            }

            var (completions, fileName, position) = _lastCompletion.Value;

            if (request.Item is null
                || request.Item.Data >= completions.Items.Length
                || request.Item.Data < 0)
            {
                _logger.LogError("Received invalid completion resolve!");
                return new CompletionResolveResponse { Item = request.Item };
            }

            var lastCompletionItem = completions.Items[request.Item.Data];
            if (lastCompletionItem.DisplayTextPrefix + lastCompletionItem.DisplayText + lastCompletionItem.DisplayTextSuffix != request.Item.Label)
            {
                _logger.LogError("Inconsistent completion data. Requested data on {0}, but found completion item {1}", request.Item.Label, lastCompletionItem.DisplayText);
                return new CompletionResolveResponse { Item = request.Item };
            }


            var document = _workspace.GetDocument(fileName);
            if (document is null)
            {
                _logger.LogInformation("Could not find document for file {0}", fileName);
                return new CompletionResolveResponse { Item = request.Item };
            }

            var completionService = CSharpCompletionService.GetService(document);

            var description = await completionService.GetDescriptionAsync(document, lastCompletionItem);

            StringBuilder textBuilder = new StringBuilder();
            MarkdownHelpers.TaggedTextToMarkdown(description.TaggedParts, textBuilder, _formattingOptions, MarkdownFormat.FirstLineAsCSharp, out _);

            request.Item.Documentation = textBuilder.ToString();

            string providerName = lastCompletionItem.GetProviderName();
            switch (providerName)
            {
                case CompletionItemExtensions.ExtensionMethodImportCompletionProvider:
                case CompletionItemExtensions.TypeImportCompletionProvider:
                    var sourceText = await document.GetTextAsync();
                    var typedSpan = completionService.GetDefaultCompletionListSpan(sourceText, position);
                    var change = await completionService.GetChangeAsync(document, lastCompletionItem, typedSpan);

                    var additionalChanges = new List<LinePositionSpanTextChange>();
                    foreach (var textChange in change.TextChanges)
                    {
                        if (textChange.NewText == request.Item.TextEdit!.NewText)
                        {
                            continue;
                        }

                        additionalChanges.Add(GetChangeForTextAndSpan(textChange.NewText, textChange.Span, sourceText));
                    }

                    request.Item.AdditionalTextEdits = additionalChanges;

                    break;
            }

            return new CompletionResolveResponse
            {
                Item = request.Item
            };
        }

        private static LinePositionSpanTextChange GetChangeForTextAndSpan(string? insertText, TextSpan changeSpan, SourceText sourceText)
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
    }
}
