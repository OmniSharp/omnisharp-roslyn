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

                //var options = await document.GetOptionsAsync();
                //options.WithChangedOption(new PerLanguageOption<SnippetsRule>("CompletionOptions", "SnippetsBehavior", SnippetsRule.AlwaysInclude), LanguageNames.CSharp, SnippetsRule.AlwaysInclude);
                //options.WithChangedOption()
                var semanticModel = await document.GetSemanticModelAsync();
                var service = CompletionService.GetService(document);
                var completionList = await service.GetCompletionsAsync(document, position);
                //options: options.WithChangedOption(new PerLanguageOption<SnippetsRule>("CompletionOptions", "SnippetsBehavior", SnippetsRule.AlwaysInclude)));

                // Add keywords from the completion list. We'll use the recommender service to get symbols
                // to create snippets from.


                var recommendedSymbols = await Recommender.GetRecommendedSymbolsAtPositionAsync(semanticModel, position, _workspace);

                if (completionList != null)
                {
                    foreach (var item in completionList.Items)
                    {
                        //if (item.Tags.Contains(CompletionTags.Keyword))
                        {
                            // Note: For keywords, we'll just assume that the completion text is the same
                            // as the display text.
                            var keyword = item.DisplayText;
                            if (keyword.IsValidCompletionFor(wordToComplete))
                            {
                                if (item.Properties.ContainsKey("Provider") && item.Properties["Provider"] == "Microsoft.CodeAnalysis.CSharp.Completion.Providers.SymbolCompletionProvider")
                                {
                                    //var symbolsTask = GetSymbolsAsync.Invoke(null, new object[] { item, document, default(CancellationToken) }) as Task<ImmutableArray<ISymbol>>;
                                    //var symbols = await symbolsTask;
                                    //SymbolFinder.
                                    //foreach (var symbol in symbols.Where(s => s.Name.IsValidCompletionFor(wordToComplete)))
                                    //service.GetDescriptionAsync
                                    {
                                        var symbols = recommendedSymbols.Where(x => x.Name == item.Properties["SymbolName"] && (int)x.Kind == int.Parse(item.Properties["SymbolKind"])).Distinct();

                                        if (symbols != null && symbols.Any())
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

                                            continue;
                                        }
                                    }
                                }

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

                //var model = await document.GetSemanticModelAsync();
                //var symbols = await Recommender.GetRecommendedSymbolsAtPositionAsync(model, position, _workspace);

                //foreach (var symbol in symbols.Where(s => s.Name.IsValidCompletionFor(wordToComplete)))
                //{
                //    if (request.WantSnippet)
                //    {
                //        foreach (var completion in MakeSnippetedResponses(request, symbol))
                //        {
                //            completions.Add(completion);
                //        }
                //    }
                //    else
                //    {
                //        completions.Add(MakeAutoCompleteResponse(request, symbol));
                //    }
                //}
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
