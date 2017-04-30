using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.AutoComplete;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Documentation;

namespace OmniSharp.Roslyn.CSharp.Services.Intellisense
{
    internal static class CompletionItemExtensions
    {
        public static IEnumerable<ISymbol> GetCompletionSymbols(this CompletionItem completionItem, IEnumerable<ISymbol> recommendedSymbols)
        {
            // for SymbolCompletionProvider, use the logic of extracting information from recommended symbols
            if (completionItem.Properties.ContainsKey("Provider") && completionItem.Properties["Provider"] == "Microsoft.CodeAnalysis.CSharp.Completion.Providers.SymbolCompletionProvider")
            {
                var symbols = recommendedSymbols.Where(x => x.Name == completionItem.Properties["SymbolName"] && (int)x.Kind == int.Parse(completionItem.Properties["SymbolKind"])).Distinct();
                return symbols ?? Enumerable.Empty<ISymbol>();
            }

            return Enumerable.Empty<ISymbol>();
        }
    }

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
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
                var semanticModel = await document.GetSemanticModelAsync();
                var service = CompletionService.GetService(document);
                var completionList = await service.GetCompletionsAsync(document, position);

                if (completionList != null)
                {
                    // get recommened symbols to match them up later with SymbolCompletionProvider
                    var recommendedSymbols = await Recommender.GetRecommendedSymbolsAtPositionAsync(semanticModel, position, _workspace);

                    foreach (var item in completionList.Items)
                    {
                        var completionText = item.DisplayText;
                        if (completionText.IsValidCompletionFor(wordToComplete))
                        {
                            var symbols = item.GetCompletionSymbols(recommendedSymbols);
                            if (symbols.Any())
                            {
                                foreach (var symbol in symbols)
                                {
                                    if (symbol != null)
                                    {
                                        if (request.WantSnippet)
                                        {
                                            foreach (var completion in MakeSnippetedResponses(request, symbol))
                                            {
                                                completions.Add(completion);
                                            }
                                        }
                                        else
                                        {
                                            completions.Add(MakeAutoCompleteResponse(request, symbol));
                                        }
                                    }
                                }

                                // if we had any symbols from the completion, we can continue, otherwise it means
                                // the completion didn't have an associated symbol so we'll add it manually
                                continue;
                            }

                            // for other completions, i.e. keywords, create a simple AutoCompleteResponse
                            // we'll just assume that the completion text is the same
                            // as the display text.
                            var response = new AutoCompleteResponse()
                            {
                                CompletionText = item.DisplayText,
                                DisplayText = item.DisplayText,
                                Snippet = item.DisplayText,
                                Kind = request.WantKind ? item.Tags.First() : null
                            };

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
                .ThenBy(c => c.CompletionText);
        }

        private IEnumerable<AutoCompleteResponse> MakeSnippetedResponses(AutoCompleteRequest request, ISymbol symbol)
        {
            var completions = new List<AutoCompleteResponse>();

            var methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                if (methodSymbol.Parameters.Any(p => p.IsOptional))
                {
                    completions.Add(MakeAutoCompleteResponse(request, symbol, false));
                }

                completions.Add(MakeAutoCompleteResponse(request, symbol));

                return completions;
            }

            var typeSymbol = symbol as INamedTypeSymbol;
            if (typeSymbol != null)
            {
                completions.Add(MakeAutoCompleteResponse(request, symbol));

                if (typeSymbol.TypeKind != TypeKind.Enum)
                {
                    foreach (var ctor in typeSymbol.InstanceConstructors)
                    {
                        completions.Add(MakeAutoCompleteResponse(request, ctor));
                    }
                }

                return completions;
            }

            return new[] { MakeAutoCompleteResponse(request, symbol) };
        }

        private AutoCompleteResponse MakeAutoCompleteResponse(AutoCompleteRequest request, ISymbol symbol, bool includeOptionalParams = true)
        {
            var displayNameGenerator = new SnippetGenerator();
            displayNameGenerator.IncludeMarkers = false;
            displayNameGenerator.IncludeOptionalParameters = includeOptionalParams;

            var response = new AutoCompleteResponse();
            response.CompletionText = symbol.Name;

            // TODO: Do something more intelligent here
            response.DisplayText = displayNameGenerator.Generate(symbol);

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

            return response;
        }
    }
}
