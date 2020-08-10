#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Helpers;
using OmniSharp.Roslyn.CSharp.Services.Intellisense;
using OmniSharp.Utilities;
using CompletionItem = OmniSharp.Models.v1.Completion.CompletionItem;
using CompletionTriggerKind = OmniSharp.Models.v1.Completion.CompletionTriggerKind;
using CSharpCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
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
            { WellKnownTags.Namespace, CompletionItemKind.Text },
            { WellKnownTags.Method, CompletionItemKind.Method },
            { WellKnownTags.Module, CompletionItemKind.Module },
            { WellKnownTags.Operator, CompletionItemKind.Operator },
            { WellKnownTags.Parameter, CompletionItemKind.Value },
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

        private (CSharpCompletionList Completions, string FileName, int Position)? _lastCompletion = null;

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
            _lastCompletion = null;

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

            var completions = await completionService.GetCompletionsAsync(document, position, getCompletionTrigger(includeTriggerCharacter: false));
            _logger.LogTrace("Found {0} completions for {1}:{2},{3}",
                             completions?.Items.IsDefaultOrEmpty != true ? 0 : completions.Items.Length,
                             request.FileName,
                             request.Line,
                             request.Column);

            if (completions is null)
            {
                return new CompletionResponse { Items = ImmutableArray<CompletionItem>.Empty };
            }

            var typedSpan = completionService.GetDefaultCompletionListSpan(sourceText, position);
            string typedText = sourceText.GetSubText(typedSpan).ToString();

            ImmutableArray<string> filteredItems = typedText != string.Empty
                ? completionService.FilterItems(document, completions.Items, typedText).SelectAsArray(i => i.DisplayText)
                : ImmutableArray<string>.Empty;
            _logger.LogTrace("Completions filled in");
            _lastCompletion = (completions, request.FileName, position);

            var triggerCharactersBuilder = ImmutableArray.CreateBuilder<char>(completions.Rules.DefaultCommitCharacters.Length);
            return new CompletionResponse
            {
                IsIncomplete = false,
                Items = completions.Items.SelectAsArrayWithArgumentAndIndex((completions, triggerCharactersBuilder, filteredItems), buildCompletion)
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

            static CompletionItem buildCompletion(
                CSharpCompletionItem completion,
                (CSharpCompletionList completions, ImmutableArray<char>.Builder triigerCharactersBuilder, ImmutableArray<string> filteredItems) completionsAndBuilder,
                int index)
            {
                var (completions, triggerCharactersBuilder, filteredItems) = completionsAndBuilder;
                triggerCharactersBuilder.AddRange(completions.Rules.DefaultCommitCharacters);

                foreach (var modifiedRule in completion.Rules.CommitCharacterRules)
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

                ImmutableArray<char> commitCharacters = triggerCharactersBuilder.ToImmutableAndClear();

                return new CompletionItem
                {
                    Label = completion.DisplayTextPrefix + completion.DisplayText + completion.DisplayTextSuffix,
                    InsertText = completion.TryGetInsertionText(out var insertionText) ? insertionText : completion.DisplayText,
                    SortText = completion.SortText,
                    FilterText = completion.FilterText,
                    Kind = getCompletionItemKind(completion.Tags),
                    Detail = completion.InlineDescription,
                    Data = index,
                    Preselect = completion.Rules.MatchPriority == MatchPriority.Preselect || filteredItems.Contains(completion.DisplayText),
                    CommitCharacters = commitCharacters
                };
            }
        }

        public async Task<CompletionResolveResponse> Handle(CompletionResolveRequest request)
        {
            if (_lastCompletion is null)
            {
                _logger.LogError("Cannot call completion/resolve before calling completion!");
                return new CompletionResolveResponse { Item = request.Item };
            }

            var (completions, fileName, _) = _lastCompletion.Value;

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
            MarkdownHelpers.TaggedTextToMarkdown(description.TaggedParts, textBuilder, _formattingOptions, out _);

            request.Item.Documentation = textBuilder.ToString();

            // TODO: Diff the document and fill in additionalTextEdits

            return new CompletionResolveResponse
            {
                Item = request.Item
            };
        }
    }
}
