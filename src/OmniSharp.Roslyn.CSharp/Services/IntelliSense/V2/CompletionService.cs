using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using CSharpCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace OmniSharp.Roslyn.CSharp.Services.IntelliSense.V2
{
    using Models = OmniSharp.Models.V2;
    using CompletionModels = OmniSharp.Models.V2.Completion;

    [Shared]
    [OmniSharpHandler(OmniSharpEndpoints.V2.Completion, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.V2.CompletionItemResolve, LanguageNames.CSharp)]
    public class CompletionService :
        IRequestHandler<CompletionModels.CompletionRequest, CompletionModels.CompletionResponse>,
        IRequestHandler<CompletionModels.CompletionItemResolveRequest, CompletionModels.CompletionItemResolveResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ILogger _logger;

        private (string fileName, CompletionList completionList) _lastResponse;

        [ImportingConstructor]
        public CompletionService(OmniSharpWorkspace workspace, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _logger = loggerFactory.CreateLogger<CompletionService>();
        }

        public async Task<CompletionModels.CompletionResponse> Handle(CompletionModels.CompletionRequest request)
        {
            var fileName = request.FileName;
            var position = request.Position;
            var trigger = request.Trigger.ToRoslynCompletionTrigger();

            var document = GetDocument(fileName);

            var text = await document.GetTextAsync();
            if (position < 0 || position > text.Length)
            {
                throw new ArgumentOutOfRangeException($"Invalid position: {position}. Should be within range 0 to {text.Length}");
            }

            var service = GetService(document);

            var completionList = await service.GetCompletionsAsync(document, position, trigger);
            if (completionList == null)
            {
                return CompletionModels.CompletionResponse.Empty;
            }

            var isSuggestionMode = completionList.SuggestionModeItem != null;

            int itemCount = completionList.Items.Length;
            var items = new CompletionModels.CompletionItem[itemCount];

            for (int i = 0; i < itemCount; i++)
            {
                var item = completionList.Items[i];

                items[i] = new CompletionModels.CompletionItem
                {
                    DisplayText = item.DisplayText,
                    Kind = item.GetKind(),
                    FilterText = item.FilterText,
                    SortText = item.SortText,
                    CommitCharacterRules = GetCommitCharacterRulesModels(item.Rules.CommitCharacterRules)
                };
            }

            var response = new CompletionModels.CompletionResponse
            {
                DefaultCommitCharacters = completionList.Rules.DefaultCommitCharacters.ToArray(),
                IsSuggestionMode = isSuggestionMode,
                Items = items
            };

            _lastResponse = (fileName, completionList);

            return response;
        }

        private static CompletionModels.CharacterSetModificationRule[] GetCommitCharacterRulesModels(ImmutableArray<CharacterSetModificationRule> commitCharacterRules)
        {
            var result = commitCharacterRules.Length > 0
                ? new CompletionModels.CharacterSetModificationRule[commitCharacterRules.Length]
                : Array.Empty<CompletionModels.CharacterSetModificationRule>();

            for (int i = 0; i < commitCharacterRules.Length; i++)
            {
                var rule = commitCharacterRules[i];
                result[i] = new CompletionModels.CharacterSetModificationRule
                {
                    Characters = rule.Characters.ToArray(),
                    Kind = (CompletionModels.CharacterSetModificationRuleKind)rule.Kind
                };
            }

            return result;
        }

        private static CSharpCompletionService GetService(Document document)
        {
            var service = CSharpCompletionService.GetService(document);
            if (service == null)
            {
                throw new InvalidOperationException("Could not retrieve Roslyn CompletionService.");
            }

            return service;
        }

        private Document GetDocument(string fileName)
        {
            var document = _workspace.GetDocument(fileName);
            if (document == null)
            {
                throw new ArgumentException($"Could not find document for {fileName}.");
            }

            return document;
        }

        public async Task<CompletionModels.CompletionItemResolveResponse> Handle(CompletionModels.CompletionItemResolveRequest request)
        {
            if (_lastResponse.fileName == null || _lastResponse.completionList == null)
            {
                throw new InvalidOperationException($"{OmniSharpEndpoints.V2.CompletionItemResolve} end point cannot be called before {OmniSharpEndpoints.V2.Completion}");
            }

            var previousFileName = _lastResponse.fileName;
            var previousCompletionList = _lastResponse.completionList;

            var fileName = request.FileName;
            var itemIndex = request.ItemIndex;
            var displayText = request.DisplayText;

            if (!string.Equals(_lastResponse.fileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Cannot resolve completion item from '{fileName}' because the last {OmniSharpEndpoints.V2.Completion} request was for '{previousFileName}'");
            }

            if (itemIndex < 0 || itemIndex >= previousCompletionList.Items.Length)
            {
                throw new ArgumentOutOfRangeException($"Invalid item index: {itemIndex}. Should be within range 0 to {previousCompletionList.Items.Length}");
            }

            var previousItem = previousCompletionList.Items[itemIndex];
            if (!string.Equals(previousItem.DisplayText, displayText))
            {
                throw new ArgumentException($"Cannot resolve completion item. Display text does not match. Expected '{previousItem.DisplayText}' but was '{displayText}'");
            }

            var document = GetDocument(fileName);
            var service = GetService(document);

            var description = await service.GetDescriptionAsync(document, previousItem);

            // CompletionService.GetChangeAsync(...) can optionally take the commit character which might be
            // used to produce a slightly different change. Unfortunately, most editors don't provide this.
            var change = await service.GetChangeAsync(document, previousItem);

            var text = await document.GetTextAsync();

            var startTextLine = text.Lines.GetLineFromPosition(change.TextChange.Span.Start);
            var startPoint = new Models.Point
            {
                Line = startTextLine.LineNumber,
                Column = change.TextChange.Span.Start - startTextLine.Start
            };

            var endTextLine = text.Lines.GetLineFromPosition(change.TextChange.Span.End);
            var endPoint = new Models.Point
            {
                Line = endTextLine.LineNumber,
                Column = change.TextChange.Span.End - endTextLine.Start
            };

            return new CompletionModels.CompletionItemResolveResponse
            {
                Item = new CompletionModels.CompletionItem
                {
                    DisplayText = previousItem.DisplayText,
                    Kind = previousItem.GetKind(),
                    FilterText = previousItem.FilterText,
                    SortText = previousItem.SortText,
                    CommitCharacterRules = GetCommitCharacterRulesModels(previousItem.Rules.CommitCharacterRules),
                    Description = description.Text,
                    TextEdit = new Models.TextEdit
                    {
                        NewText = change.TextChange.NewText,
                        Range = new Models.Range
                        {
                            Start = startPoint,
                            End = endPoint
                        }
                    }
                }
            };
        }
    }
}
