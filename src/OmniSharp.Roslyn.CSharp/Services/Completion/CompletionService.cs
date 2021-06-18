#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Completion;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Helpers;
using OmniSharp.Utilities;
using CompletionItem = OmniSharp.Models.v1.Completion.CompletionItem;
using CompletionTriggerKind = OmniSharp.Models.v1.Completion.CompletionTriggerKind;
using CSharpCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace OmniSharp.Roslyn.CSharp.Services.Completion
{
    [Shared]
    [OmniSharpHandler(OmniSharpEndpoints.Completion, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.CompletionResolve, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.CompletionAfterInsert, LanguageNames.CSharp)]
    public class CompletionService :
        IRequestHandler<CompletionRequest, CompletionResponse>,
        IRequestHandler<CompletionResolveRequest, CompletionResolveResponse>,
        IRequestHandler<CompletionAfterInsertRequest, CompletionAfterInsertResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly OmniSharpOptions _omniSharpOptions;
        private readonly FormattingOptions _formattingOptions;
        private readonly ILogger _logger;

        private readonly CompletionListCache _cache = new();

        [ImportingConstructor]
        public CompletionService(OmniSharpWorkspace workspace, FormattingOptions formattingOptions, ILoggerFactory loggerFactory, OmniSharpOptions omniSharpOptions)
        {
            _workspace = workspace;
            _formattingOptions = formattingOptions;
            _logger = loggerFactory.CreateLogger<CompletionService>();
            _omniSharpOptions = omniSharpOptions;
        }

        public async Task<CompletionResponse> Handle(CompletionRequest request)
        {
            _logger.LogTrace("Completions requested");

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
                CompletionTriggerKind.TriggerCharacter when request.TriggerCharacter is char c => CompletionTrigger.CreateInsertionTrigger(c),
                _ => CompletionTrigger.Invoke,
            };

            if (request.CompletionTrigger == CompletionTriggerKind.TriggerCharacter &&
                !completionService.ShouldTriggerCompletion(sourceText, position, trigger))
            {
                _logger.LogTrace("Should not insert completions here.");
                return new CompletionResponse { Items = ImmutableArray<CompletionItem>.Empty };
            }

            var (completions, expandedItemsAvailable) = await OmniSharpCompletionService.GetCompletionsAsync(completionService, document, position, trigger);
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
                return providerName is CompletionListBuilder.OverrideCompletionProvider or
                                       CompletionListBuilder.PartialMethodCompletionProvider or
                                       CompletionListBuilder.ObjectCreationCompletionProvider;
            }))
            {
                // Only trigger on space if there is an object creation completion
                return new CompletionResponse { Items = ImmutableArray<CompletionItem>.Empty };
            }

            var typedSpan = completionService.GetDefaultCompletionListSpan(sourceText, position);
            string typedText = sourceText.GetSubText(typedSpan).ToString();
            _logger.LogTrace("Completions filled in");
            var cacheId = _cache.UpdateCache(document, position, completions);


            // If we don't encounter any unimported types, and the completion context thinks that some would be available, then
            // that completion provider is still creating the cache. We'll mark this completion list as not completed, and the
            // editor will ask again when the user types more. By then, hopefully the cache will have populated and we can mark
            // the completion as done.
            bool expectingImportedItems = expandedItemsAvailable && _workspace.Options.GetOption(OmniSharpCompletionService.ShowItemsFromUnimportedNamespaces, LanguageNames.CSharp) == true;
            var syntax = await document.GetSyntaxTreeAsync();

            var replacingSpanStartPosition = sourceText.Lines.GetLinePosition(typedSpan.Start);
            var replacingSpanEndPosition = sourceText.Lines.GetLinePosition(typedSpan.End);
            var isSuggestionMode = completions.SuggestionModeItem is not null;


            var (completionsList, seenUnimportedCompletions) = await CompletionListBuilder.BuildCompletionItems(
                document,
                sourceText,
                cacheId,
                position,
                completionService,
                completions,
                typedSpan,
                expectingImportedItems,
                isSuggestionMode,
                _omniSharpOptions.RoslynExtensionsOptions.EnableAsyncCompletion);

            return new CompletionResponse
            {
                IsIncomplete = !seenUnimportedCompletions && expectingImportedItems,
                Items = completionsList
            };
        }

        public async Task<CompletionResolveResponse> Handle(CompletionResolveRequest request)
        {
            var cachedList = _cache.GetCachedCompletionList(request.Item.Data.CacheId);
            if (cachedList is null)
            {
                _logger.LogError("Cannot call completion/resolve before calling completion!");
                return new CompletionResolveResponse { Item = request.Item };
            }

            var (_, document, position, completions) = cachedList;
            var index = request.Item.Data.Index;

            if (request.Item is null
                || index >= completions.Items.Length
                || index < 0)
            {
                _logger.LogError("Received invalid completion resolve!");
                return new CompletionResolveResponse { Item = request.Item };
            }

            var lastCompletionItem = completions.Items[index];
            if (lastCompletionItem.DisplayTextPrefix + lastCompletionItem.DisplayText + lastCompletionItem.DisplayTextSuffix != request.Item.Label)
            {
                _logger.LogError("Inconsistent completion data. Requested data on {0}, but found completion item {1}", request.Item.Label, lastCompletionItem.DisplayText);
                return new CompletionResolveResponse { Item = request.Item };
            }

            var completionService = CSharpCompletionService.GetService(document);
            var description = await completionService.GetDescriptionAsync(document, lastCompletionItem);

            var textBuilder = new StringBuilder();
            MarkdownHelpers.TaggedTextToMarkdown(description.TaggedParts, textBuilder, _formattingOptions, MarkdownFormat.FirstLineAsCSharp, out _);

            request.Item.Documentation = textBuilder.ToString();

            string providerName = lastCompletionItem.GetProviderName();
            switch (providerName)
            {
                case CompletionListBuilder.ExtensionMethodImportCompletionProvider:
                case CompletionListBuilder.TypeImportCompletionProvider:
                    var sourceText = await document.GetTextAsync();
                    var change = await completionService.GetChangeAsync(document, lastCompletionItem);

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

        public async Task<CompletionAfterInsertResponse> Handle(CompletionAfterInsertRequest request)
        {
            var cachedList = _cache.GetCachedCompletionList(request.Item.Data.CacheId);
            if (cachedList is null)
            {
                _logger.LogError("Cannot call completion/afterInsert before calling completion!");
                return new CompletionAfterInsertResponse();
            }

            var (_, document, _, completions) = cachedList;
            var index = request.Item.Data.Index;

            if (request.Item is null
                || index >= completions.Items.Length
                || index < 0
                || request.Item.TextEdit is null)
            {
                _logger.LogError("Received invalid completion afterInsert!");
                return new CompletionAfterInsertResponse();
            }

            var lastCompletionItem = completions.Items[index];
            if (lastCompletionItem.DisplayTextPrefix + lastCompletionItem.DisplayText + lastCompletionItem.DisplayTextSuffix != request.Item.Label)
            {
                _logger.LogError("Inconsistent completion data. Requested data on {0}, but found completion item {1}", request.Item.Label, lastCompletionItem.DisplayText);
                return new CompletionAfterInsertResponse();
            }

            if (lastCompletionItem.GetProviderName() is not (CompletionListBuilder.OverrideCompletionProvider or
                                                             CompletionListBuilder.PartialMethodCompletionProvider)
                                                        and var name)
            {
                _logger.LogWarning("Received unsupported afterInsert completion request for provider {0}", name);
                return new CompletionAfterInsertResponse();
            }

            var completionService = CSharpCompletionService.GetService(document);

            // Get a document with change from the completion inserted, so that we can resolve the completion and get the
            // final full change.
            var sourceText = await document.GetTextAsync();
            var insertedSpan = sourceText.GetSpanFromLinePositionSpanTextChange(request.Item.TextEdit);
            var changedText = sourceText.WithChanges(new TextChange(insertedSpan, request.Item.TextEdit.NewText));
            var changedDocument = document.WithText(changedText);

            var finalChange = await completionService.GetChangeAsync(changedDocument, lastCompletionItem);
            var finalText = changedText.WithChanges(finalChange.TextChange);
            var finalPosition = finalText.GetPointFromPosition(finalChange.NewPosition!.Value);

            return new CompletionAfterInsertResponse
            {
                Changes = finalChange.TextChanges.SelectAsArray(changedText, static (c, changedText) => CompletionListBuilder.GetChangeForTextAndSpan(c.NewText, c.Span, changedText)),
                Line = finalPosition.Line,
                Column = finalPosition.Column,
            };
        }
    }
}
