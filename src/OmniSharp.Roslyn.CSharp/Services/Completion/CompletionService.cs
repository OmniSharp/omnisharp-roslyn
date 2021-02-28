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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        private static readonly Dictionary<string, CompletionItemKind> s_roslynTagToCompletionItemKind = new Dictionary<string, CompletionItemKind>()
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

        private readonly OmniSharpWorkspace _workspace;
        private readonly FormattingOptions _formattingOptions;
        private readonly ILogger _logger;

        private readonly object _lock = new object();
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

            if (request.CompletionTrigger == CompletionTriggerKind.TriggerCharacter &&
                !completionService.ShouldTriggerCompletion(sourceText, position, getCompletionTrigger(includeTriggerCharacter: true)))
            {
                _logger.LogTrace("Should not insert completions here.");
                return new CompletionResponse { Items = ImmutableArray<CompletionItem>.Empty };
            }

            var (completions, expandedItemsAvailable) = await completionService.GetCompletionsInternalAsync(
                document,
                position,
                getCompletionTrigger(includeTriggerCharacter: false));
            _logger.LogTrace("Found {0} completions for {1}:{2},{3}",
                             completions?.Items.IsDefaultOrEmpty != true ? 0 : completions.Items.Length,
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
                return providerName == CompletionItemExtensions.OverrideCompletionProvider ||
                       providerName == CompletionItemExtensions.PartialMethodCompletionProvider ||
                       providerName == CompletionItemExtensions.ObjectCreationCompletionProvider;
            }))
            {
                // Only trigger on space if there is an object creation completion
                return new CompletionResponse { Items = ImmutableArray<CompletionItem>.Empty };
            }

            var typedSpan = completionService.GetDefaultCompletionListSpan(sourceText, position);
            string typedText = sourceText.GetSubText(typedSpan).ToString();

            ImmutableArray<string> filteredItems = typedText != string.Empty
                ? completionService.FilterItems(document, completions.Items, typedText).SelectAsArray(i => i.DisplayText)
                : ImmutableArray<string>.Empty;
            _logger.LogTrace("Completions filled in");

            lock (_lock)
            {
                _lastCompletion = (completions, request.FileName, position);
            }

            var triggerCharactersBuilder = ImmutableArray.CreateBuilder<char>(completions.Rules.DefaultCommitCharacters.Length);
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

            for (int i = 0; i < completions.Items.Length; i++)
            {
                var completion = completions.Items[i];
                var insertTextFormat = InsertTextFormat.PlainText;
                IReadOnlyList<LinePositionSpanTextChange>? additionalTextEdits = null;
                char sortTextPrepend = '0';

                if (!completion.TryGetInsertionText(out string insertText))
                {
                    string providerName = completion.GetProviderName();
                    switch (providerName)
                    {
                        case CompletionItemExtensions.EmeddedLanguageCompletionProvider:
                            // The Regex completion provider can change escapes based on whether
                            // we're in a verbatim string or not
                            {
                                CompletionChange change = await completionService.GetChangeAsync(document, completion);
                                Debug.Assert(typedSpan == change.TextChange.Span);
                                insertText = change.TextChange.NewText!;
                            }
                            break;

                        case CompletionItemExtensions.InternalsVisibleToCompletionProvider:
                            // The IVT completer doesn't add extra things before the completion
                            // span, only assembly keys at the end if they exist.
                            {
                                // if the completion is for the hidden Misc files project, skip it
                                if (completion.DisplayText == Configuration.OmniSharpMiscProjectName) continue;
                                CompletionChange change = await completionService.GetChangeAsync(document, completion);
                                Debug.Assert(typedSpan == change.TextChange.Span);
                                insertText = change.TextChange.NewText!;
                            }
                            break;

                        case CompletionItemExtensions.XmlDocCommentCompletionProvider:
                            {
                                // The doc comment completion might compensate for the < before
                                // the current word, if one exists. For these cases, if the token
                                // before the current location is a < and the text it's replacing starts
                                // with a <, erase the < from the given insertion text.
                                var change = await completionService.GetChangeAsync(document, completion);

                                bool trimFront = change.TextChange.NewText![0] == '<'
                                                 && sourceText[change.TextChange.Span.Start] == '<';

                                Debug.Assert(!trimFront || change.TextChange.Span.Start + 1 == typedSpan.Start);

                                (insertText, insertTextFormat) = getAdjustedInsertTextWithPosition(change, position, newOffset: trimFront ? 1 : 0);
                            }
                            break;

                        case CompletionItemExtensions.OverrideCompletionProvider:
                        case CompletionItemExtensions.PartialMethodCompletionProvider:
                            {
                                // For these two, we potentially need to use additionalTextEdits. It's possible
                                // that override (or C# expanded partials) will cause the word or words before
                                // the cursor to be adjusted. For example:
                                //
                                // public class C {
                                //     override $0
                                // }
                                //
                                // Invoking completion and selecting, say Equals, wants to cause the line to be
                                // rewritten as this:
                                //
                                // public class C {
                                //     public override bool Equals(object other)
                                //     {
                                //         return base.Equals(other);$0
                                //     }
                                // }
                                //
                                // In order to handle this, we need to chop off the section of the completion
                                // before the cursor and bundle that into an additionalTextEdit. Then, we adjust
                                // the remaining bit of the change to put the cursor in the expected spot via
                                // snippets. We could leave the additionalTextEdit bit for resolve, but we already
                                // have the data do the change and we basically have to compute the whole thing now
                                // anyway, so it doesn't really save us anything.

                                var change = await completionService.GetChangeAsync(document, completion);

                                if (typedSpan == change.TextChange.Span)
                                {
                                    // If the span we're using to key the completion off is the same as the replacement
                                    // span, then we don't need to do anything special, just snippitize the text and
                                    // exit
                                    (insertText, insertTextFormat) = getAdjustedInsertTextWithPosition(change, position, newOffset: 0);
                                    break;
                                }

                                if (change.TextChange.Span.Start > typedSpan.Start)
                                {
                                    // If the span we're using to key the replacement span is within the original typed span
                                    // span, we want to prepend the missing text from the original typed text to here. The
                                    // reason is that some lsp clients, such as vscode, use the range from the text edit as
                                    // the selector for what filter text to use. This can lead to odd scenarios where invoking
                                    // completion and typing `EQ` will bring up the Equals override, but then dismissing and
                                    // reinvoking completion will have a range that just replaces the Q. Vscode will then consider
                                    // that capital Q to be the start of the filter text, and filter out the Equals overload
                                    // leaving the user with no completion and no explanation
                                    Debug.Assert(change.TextChange.Span.End == typedSpan.End);
                                    var prefix = typedText.Substring(0, change.TextChange.Span.Start - typedSpan.Start);

                                    (insertText, insertTextFormat) = getAdjustedInsertTextWithPosition(change, position, newOffset: 0, prefix);
                                    break;
                                }

                                int additionalEditEndOffset;
                                (additionalTextEdits, additionalEditEndOffset) = GetAdditionalTextEdits(change, sourceText, (CSharpParseOptions)syntax!.Options, typedSpan, completion.DisplayText, providerName);

                                if (additionalEditEndOffset < 0)
                                {
                                    // We couldn't find the position of the change in the new text. This shouldn't happen in normal cases,
                                    // but handle as best we can in release
                                    Debug.Fail("Couldn't find the new cursor position in the replacement text!");
                                    additionalTextEdits = null;
                                    insertText = completion.DisplayText;
                                    break;
                                }

                                // Now that we have the additional edit, adjust the rest of the new text
                                (insertText, insertTextFormat) = getAdjustedInsertTextWithPosition(change, position, additionalEditEndOffset);
                            }
                            break;

                        case CompletionItemExtensions.TypeImportCompletionProvider:
                        case CompletionItemExtensions.ExtensionMethodImportCompletionProvider:
                            // We did indeed find unimported types, the completion list can be considered complete.
                            // This is technically slightly incorrect: extension method completion can provide
                            // partial results. However, this should only affect the first completion session or
                            // two and isn't a big problem in practice.
                            seenUnimportedCompletions = true;
                            sortTextPrepend = '1';
                            goto default;

                        default:
                            insertText = completion.DisplayText;
                            break;
                    }
                }

                var commitCharacters = buildCommitCharacters(completions, completion.Rules.CommitCharacterRules, triggerCharactersBuilder);

                completionsBuilder.Add(new CompletionItem
                {
                    Label = completion.DisplayTextPrefix + completion.DisplayText + completion.DisplayTextSuffix,
                    TextEdit = new LinePositionSpanTextChange
                    {
                        NewText = insertText,
                        StartLine = replacingSpanStartPosition.Line,
                        StartColumn = replacingSpanStartPosition.Character,
                        EndLine = replacingSpanEndPosition.Line,
                        EndColumn = replacingSpanEndPosition.Character
                    },
                    InsertTextFormat = insertTextFormat,
                    AdditionalTextEdits = additionalTextEdits,
                    // Ensure that unimported items are sorted after things already imported.
                    SortText = expectingImportedItems ? sortTextPrepend + completion.SortText : completion.SortText,
                    FilterText = completion.FilterText,
                    Kind = getCompletionItemKind(completion.Tags),
                    Detail = completion.InlineDescription,
                    Data = i,
                    Preselect = completion.Rules.MatchPriority == MatchPriority.Preselect || filteredItems.Contains(completion.DisplayText),
                    CommitCharacters = commitCharacters,
                });
            }

            return new CompletionResponse
            {
                IsIncomplete = !seenUnimportedCompletions && expectingImportedItems,
                Items = completionsBuilder.Capacity == completionsBuilder.Count ? completionsBuilder.MoveToImmutable() : completionsBuilder.ToImmutable()
            };

            CompletionTrigger getCompletionTrigger(bool includeTriggerCharacter)
                => request.CompletionTrigger switch
                {
                    CompletionTriggerKind.Invoked => CompletionTrigger.Invoke,
                    // https://github.com/dotnet/roslyn/issues/42982: Passing a trigger character
                    // to GetCompletionsAsync causes a null ref currently.
                    CompletionTriggerKind.TriggerCharacter when includeTriggerCharacter => CompletionTrigger.CreateInsertionTrigger((char)request.TriggerCharacter!),
                    _ => CompletionTrigger.Invoke,
                };

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

            static ImmutableArray<char> buildCommitCharacters(CSharpCompletionList completions, ImmutableArray<CharacterSetModificationRule> characterRules, ImmutableArray<char>.Builder triggerCharactersBuilder)
            {
                triggerCharactersBuilder.AddRange(completions.Rules.DefaultCommitCharacters);

                foreach (var modifiedRule in characterRules)
                {
                    switch (modifiedRule.Kind)
                    {
                        case CharacterSetModificationKind.Add:
                            triggerCharactersBuilder.AddRange(modifiedRule.Characters);
                            break;

                        case CharacterSetModificationKind.Remove:
                            for (int i = 0; i < triggerCharactersBuilder.Count; i++)
                            {
                                if (modifiedRule.Characters.Contains(triggerCharactersBuilder[i]))
                                {
                                    triggerCharactersBuilder.RemoveAt(i);
                                    i--;
                                }
                            }

                            break;

                        case CharacterSetModificationKind.Replace:
                            triggerCharactersBuilder.Clear();
                            triggerCharactersBuilder.AddRange(modifiedRule.Characters);
                            break;
                    }
                }

                // VS has a more complex concept of a commit mode vs suggestion mode for intellisense.
                // LSP doesn't have this, so mock it as best we can by removing space ` ` from the list
                // of commit characters if we're in suggestion mode.
                if (completions.SuggestionModeItem is object)
                {
                    triggerCharactersBuilder.Remove(' ');
                }

                return triggerCharactersBuilder.ToImmutableAndClear();
            }

            static (string, InsertTextFormat) getAdjustedInsertTextWithPosition(
                CompletionChange change,
                int originalPosition,
                int newOffset,
                string? prependText = null)
            {
                // We often have to trim part of the given change off the front, but we
                // still want to turn the resulting change into a snippet and control
                // the cursor location in the insertion text. We therefore need to compensate
                // by cutting off the requested portion of the text, finding the adjusted
                // position in the requested string, and snippetizing it.

                // NewText is annotated as nullable, but this is a misannotation that will be fixed.
                string newText = change.TextChange.NewText!;

                // Easy-out, either Roslyn doesn't have an opinion on adjustment, or the adjustment is after the
                // end of the new text. Just return a substring from the requested offset to the end
                if (!(change.NewPosition is int newPosition)
                    || newPosition >= (change.TextChange.Span.Start + newText.Length))
                {
                    return (prependText + newText.Substring(newOffset), InsertTextFormat.PlainText);
                }

                // Roslyn wants to move the cursor somewhere inside the result. Substring from the
                // requested start to the new position, and from the new position to the end of the
                // string.
                int midpoint = newPosition - change.TextChange.Span.Start;
                var beforeText = LspSnippetHelpers.Escape(prependText + newText.Substring(newOffset, midpoint - newOffset));
                var afterText = LspSnippetHelpers.Escape(newText.Substring(midpoint));

                return (beforeText + "$0" + afterText, InsertTextFormat.Snippet);
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
                _logger.LogError($"Inconsistent completion data. Requested data on {request.Item.Label}, but found completion item {lastCompletionItem.DisplayText}");
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
                    var syntax = await document.GetSyntaxTreeAsync();
                    var sourceText = await document.GetTextAsync();
                    var typedSpan = completionService.GetDefaultCompletionListSpan(sourceText, position);
                    var change = await completionService.GetChangeAsync(document, lastCompletionItem, typedSpan);
                    var (additionalTextEdits, offset) = GetAdditionalTextEdits(change, sourceText, (CSharpParseOptions)syntax!.Options, typedSpan, lastCompletionItem.DisplayText, providerName);
                    if (offset > 0)
                    {
                        Debug.Assert(additionalTextEdits is object);
                        request.Item.AdditionalTextEdits = additionalTextEdits;
                    }
                    break;
            }

            return new CompletionResolveResponse
            {
                Item = request.Item
            };
        }

        private (IReadOnlyList<LinePositionSpanTextChange>? edits, int endOffset) GetAdditionalTextEdits(
            CompletionChange change,
            SourceText sourceText,
            CSharpParseOptions parseOptions,
            TextSpan typedSpan,
            string completionDisplayText,
            string providerName)
        {
            // We know the span starts before the text we're keying off of. So, break that
            // out into a separate edit. We need to cut out the space before the current word,
            // as the additional edit is not allowed to overlap with the insertion point.
            var additionalEditStartPosition = sourceText.Lines.GetLinePosition(change.TextChange.Span.Start);
            var additionalEditEndPosition = sourceText.Lines.GetLinePosition(typedSpan.Start - 1);
            int additionalEditEndOffset = getAdditionalTextEditEndOffset(change, sourceText, parseOptions, completionDisplayText, providerName);
            if (additionalEditEndOffset < 1)
            {
                // The first index of this was either 0 and the edit span was wrong,
                // or it wasn't found at all. In this case, just do the best we can:
                // send the whole string wtih no additional edits and log a warning.
                _logger.LogWarning("Could not find the first index of the display text.\nDisplay text: {0}.\nCompletion Text: {1}",
                    completionDisplayText, change.TextChange.NewText);
                return (null, -1);
            }

            return (ImmutableArray.Create(new LinePositionSpanTextChange
            {
                // Again, we cut off the space at the end of the offset
                NewText = change.TextChange.NewText!.Substring(0, additionalEditEndOffset - 1),
                StartLine = additionalEditStartPosition.Line,
                StartColumn = additionalEditStartPosition.Character,
                EndLine = additionalEditEndPosition.Line,
                EndColumn = additionalEditEndPosition.Character,
            }), additionalEditEndOffset);

            static int getAdditionalTextEditEndOffset(CompletionChange change, SourceText sourceText, CSharpParseOptions parseOptions, string completionDisplayText, string providerName)
            {
                if (providerName == CompletionItemExtensions.ExtensionMethodImportCompletionProvider ||
                    providerName == CompletionItemExtensions.TypeImportCompletionProvider)
                {
                    return change.TextChange.NewText!.LastIndexOf(completionDisplayText);
                }

                // The DisplayText wasn't in the final string. This can happen in a few cases:
                //  * The override or partial method completion is involving types that need
                //    to have a using added in the final version, and won't be fully qualified
                //    as they were in the DisplayText
                //  * Nullable context differences, such as if the thing you're overriding is
                //    annotated but the final context being generated into does not have
                //    annotations enabled.
                // For these cases, we currently should only be seeing override or partial
                // completions, as import completions don't have nullable annotations or
                // fully-qualified types in their DisplayTexts. If that ever changes, we'll have
                // to adjust the API here.
                //
                // In order to find the correct location here, we parse the change. The location
                // of the method or property that contains the new cursor position is the location
                // of the new changes
                Debug.Assert(providerName == CompletionItemExtensions.OverrideCompletionProvider ||
                             providerName == CompletionItemExtensions.PartialMethodCompletionProvider);
                Debug.Assert(change.NewPosition.HasValue);

                var parsedTree = CSharpSyntaxTree.ParseText(sourceText.WithChanges(change.TextChange).ToString(), parseOptions);

                var tokenOfNewPosition = parsedTree.GetRoot().FindToken(change.NewPosition!.Value);
                var finalNode = tokenOfNewPosition.Parent;
                while (finalNode != null)
                {
                    switch (finalNode)
                    {
                        case MethodDeclarationSyntax decl:
                            return decl.Identifier.SpanStart - change.TextChange.Span.Start;
                        case PropertyDeclarationSyntax prop:
                            return prop.Identifier.SpanStart - change.TextChange.Span.Start;
                    }

                    finalNode = finalNode.Parent;
                }

                return -1;
            }
        }
    }
}
