using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Completion;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.AutoComplete;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Documentation;
using OmniSharp.Roslyn.CSharp.Services.Completion;
using CompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace OmniSharp.Roslyn.CSharp.Services.Intellisense
{
    [Obsolete("Please use CompletionService.")]
    [OmniSharpHandler(OmniSharpEndpoints.AutoComplete, LanguageNames.CSharp)]
    public class IntellisenseService : IRequestHandler<AutoCompleteRequest, IEnumerable<AutoCompleteResponse>>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly FormattingOptions _formattingOptions;

        [ImportingConstructor]
        public IntellisenseService(OmniSharpWorkspace workspace, FormattingOptions formattingOptions)
        {
            _workspace = workspace;
            _formattingOptions = formattingOptions;
        }

        public async Task<IEnumerable<AutoCompleteResponse>> Handle(AutoCompleteRequest request)
        {
            var documents = _workspace.GetDocuments(request.FileName);
            var wordToComplete = request.WordToComplete;
            var completions = new HashSet<AutoCompleteResponse>();

            foreach (var document in documents)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.GetTextPosition(request);
                var service = CompletionService.GetService(document);
                var completionList = await service.GetCompletionsAsync(document, position);

                if (completionList != null)
                {
                    // Only trigger on space if Roslyn has object creation items
                    if (request.TriggerCharacter == " " && !completionList.Items.Any(i => i.GetProviderName() is CompletionListBuilder.ObjectCreationCompletionProvider))
                    {
                        return completions;
                    }

                    // get recommended symbols to match them up later with SymbolCompletionProvider
                    var semanticModel = await document.GetSemanticModelAsync();
                    var recommendedSymbols = (await Recommender.GetRecommendedSymbolsAtPositionAsync(semanticModel, position, _workspace)).ToArray();

                    var isSuggestionMode = completionList.SuggestionModeItem != null;
                    foreach (var item in completionList.Items)
                    {
                        var completionText = item.DisplayText;
                        var preselect = item.Rules.MatchPriority == MatchPriority.Preselect;
                        if (completionText.IsValidCompletionFor(wordToComplete))
                        {
                            var symbols = await item.GetCompletionSymbolsAsync(recommendedSymbols, document);
                            if (symbols.Any())
                            {
                                foreach (var symbol in symbols)
                                {
                                    if (item.UseDisplayTextAsCompletionText())
                                    {
                                        completionText = item.DisplayText;
                                    }
                                    else if (item.TryGetInsertionText(out var insertionText))
                                    {
                                        completionText = insertionText;
                                    }
                                    else
                                    {
                                        completionText = symbol.Name;
                                    }

                                    if (symbol != null)
                                    {
                                        if (request.WantSnippet)
                                        {
                                            foreach (var completion in MakeSnippetedResponses(request, symbol, completionText, preselect, isSuggestionMode))
                                            {
                                                completions.Add(completion);
                                            }
                                        }
                                        else
                                        {
                                            completions.Add(MakeAutoCompleteResponse(request, symbol, completionText, preselect, isSuggestionMode));
                                        }
                                    }
                                }

                                // if we had any symbols from the completion, we can continue, otherwise it means
                                // the completion didn't have an associated symbol so we'll add it manually
                                continue;
                            }

                            // for other completions, i.e. keywords or em, create a simple AutoCompleteResponse
                            var response = item.ToAutoCompleteResponse(request.WantKind, isSuggestionMode, preselect);
                            completions.Add(response);
                        }
                    }
                }
            }

            return completions
                .OrderByDescending(c => c.CompletionText.IsValidCompletionStartsWithExactCase(wordToComplete))
                .ThenByDescending(c => c.CompletionText.IsValidCompletionStartsWithIgnoreCase(wordToComplete))
                .ThenByDescending(c => c.CompletionText.IsCamelCaseMatch(wordToComplete))
                .ThenByDescending(c => c.CompletionText.IsSubsequenceMatch(wordToComplete))
                .ThenBy(c => c.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.CompletionText, StringComparer.OrdinalIgnoreCase);
        }

        private IEnumerable<AutoCompleteResponse> MakeSnippetedResponses(AutoCompleteRequest request, ISymbol symbol, string completionText, bool preselect, bool isSuggestionMode)
        {
            switch (symbol)
            {
                case IMethodSymbol methodSymbol:
                    return MakeSnippetedResponses(request, methodSymbol, completionText, preselect, isSuggestionMode);
                case INamedTypeSymbol typeSymbol:
                    return MakeSnippetedResponses(request, typeSymbol, completionText, preselect, isSuggestionMode);

                default:
                    return new[] { MakeAutoCompleteResponse(request, symbol, completionText, preselect, isSuggestionMode) };
            }
        }

        private IEnumerable<AutoCompleteResponse> MakeSnippetedResponses(AutoCompleteRequest request, IMethodSymbol methodSymbol, string completionText, bool preselect, bool isSuggestionMode)
        {
            var completions = new List<AutoCompleteResponse>();

            if (methodSymbol.Parameters.Any(p => p.IsOptional))
            {
                completions.Add(MakeAutoCompleteResponse(request, methodSymbol, completionText, preselect, isSuggestionMode, includeOptionalParams: false));
            }

            completions.Add(MakeAutoCompleteResponse(request, methodSymbol, completionText, preselect, isSuggestionMode));

            return completions;
        }

        private IEnumerable<AutoCompleteResponse> MakeSnippetedResponses(AutoCompleteRequest request, INamedTypeSymbol typeSymbol, string completionText, bool preselect, bool isSuggestionMode)
        {
            var completions = new List<AutoCompleteResponse>
            {
                MakeAutoCompleteResponse(request, typeSymbol, completionText, preselect, isSuggestionMode)
            };

            if (typeSymbol.TypeKind != TypeKind.Enum)
            {
                foreach (var ctor in typeSymbol.InstanceConstructors)
                {
                    completions.Add(MakeAutoCompleteResponse(request, ctor, completionText, preselect, isSuggestionMode));
                }
            }

            return completions;
        }

        private AutoCompleteResponse MakeAutoCompleteResponse(AutoCompleteRequest request, ISymbol symbol, string completionText, bool preselect, bool isSuggestionMode, bool includeOptionalParams = true)
        {
            var displayNameGenerator = new SnippetGenerator();
            displayNameGenerator.IncludeMarkers = false;
            displayNameGenerator.IncludeOptionalParameters = includeOptionalParams;

            var response = new AutoCompleteResponse();
            response.CompletionText = completionText;

            // TODO: Do something more intelligent here
            response.DisplayText = displayNameGenerator.Generate(symbol);

            response.IsSuggestionMode = isSuggestionMode;

            if (request.WantDocumentationForEveryCompletionResult)
            {
                response.Description = DocumentationConverter.ConvertDocumentation(symbol.GetDocumentationCommentXml(), _formattingOptions.NewLine);
            }

            if (request.WantReturnType)
            {
                response.ReturnType = ReturnTypeFormatter.GetReturnType(symbol);
            }

            if (request.WantKind)
            {
                response.Kind = symbol.GetKind();
            }

            if (request.WantSnippet)
            {
                var snippetGenerator = new SnippetGenerator();
                snippetGenerator.IncludeMarkers = true;
                snippetGenerator.IncludeOptionalParameters = includeOptionalParams;
                response.Snippet = snippetGenerator.Generate(symbol);
            }

            if (request.WantMethodHeader)
            {
                response.MethodHeader = displayNameGenerator.Generate(symbol);
            }

            response.Preselect = preselect;

            return response;
        }
    }
}
