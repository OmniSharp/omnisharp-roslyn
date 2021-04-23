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
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Helpers;
using OmniSharp.Roslyn.CSharp.Services.Intellisense;
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


            // If we don't encounter any unimported types, and the completion context thinks that some would be available, then
            // that completion provider is still creating the cache. We'll mark this completion list as not completed, and the
            // editor will ask again when the user types more. By then, hopefully the cache will have populated and we can mark
            // the completion as done.
            bool expectingImportedItems = expandedItemsAvailable && _workspace.Options.GetOption(CompletionItemExtensions.ShowItemsFromUnimportedNamespaces, LanguageNames.CSharp) == true;
            var syntax = await document.GetSyntaxTreeAsync();

            var replacingSpanStartPosition = sourceText.Lines.GetLinePosition(typedSpan.Start);
            var replacingSpanEndPosition = sourceText.Lines.GetLinePosition(typedSpan.End);
            var isSuggestionMode = completions.SuggestionModeItem is not null;


            var (completionsList, seenUnimportedCompletions) = await CompletionListBuilder.BuildCompletionItems(document, sourceText, position, completionService, completions, typedSpan, expectingImportedItems, isSuggestionMode);

            return new CompletionResponse
            {
                IsIncomplete = !seenUnimportedCompletions && expectingImportedItems,
                Items = completionsList
            };
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

                        additionalChanges.Add(CompletionListBuilder.GetChangeForTextAndSpan(textChange.NewText, textChange.Span, sourceText));
                    }

                    request.Item.AdditionalTextEdits = additionalChanges;

                    break;
            }

            return new CompletionResolveResponse
            {
                Item = request.Item
            };
        }
    }
}
