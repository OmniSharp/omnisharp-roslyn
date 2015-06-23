using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Documentation;
using OmniSharp.Extensions;
using OmniSharp.Intellisense;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        private HashSet<AutoCompleteResponse> _completions
            = new HashSet<AutoCompleteResponse>();

        [HttpPost("autocomplete")]
        public async Task<IEnumerable<AutoCompleteResponse>> AutoComplete(AutoCompleteRequest request)
        {
            var documents = _workspace.GetDocuments(request.FileName);
            var wordToComplete = request.WordToComplete;

            foreach (var document in documents)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var model = await document.GetSemanticModelAsync();

                AddKeywords(model, position, request.WantKind, wordToComplete);

                var symbols = Recommender.GetRecommendedSymbolsAtPosition(model, position, _workspace);

                foreach (var symbol in symbols.Where(s => s.Name.IsValidCompletionFor(wordToComplete)))
                {
                    if (request.WantSnippet)
                    {
                        foreach (var completion in MakeSnippetedResponses(request, symbol))
                        {
                            _completions.Add(completion);
                        }
                    }
                    else
                    {
                        _completions.Add(MakeAutoCompleteResponse(request, symbol));
                    }
                }
            }

            return _completions
                .OrderByDescending(c => c.CompletionText.IsValidCompletionStartsWithExactCase(wordToComplete))
                .ThenByDescending(c => c.CompletionText.IsValidCompletionStartsWithIgnoreCase(wordToComplete))
                .ThenByDescending(c => c.CompletionText.IsCamelCaseMatch(wordToComplete))
                .ThenByDescending(c => c.CompletionText.IsSubsequenceMatch(wordToComplete))
                .ThenBy(c => c.CompletionText);
        }

        private void AddKeywords(SemanticModel model, int position, bool wantKind, string wordToComplete)
        {
            var context = CSharpSyntaxContext.CreateContext(_workspace, model, position, CancellationToken.None);
            var keywordHandler = new KeywordContextHandler();
            var keywords = keywordHandler.Get(context, model, position);

            foreach (var keyword in keywords.Where(k => k.IsValidCompletionFor(wordToComplete)))
            {
                _completions.Add(new AutoCompleteResponse
                {
                    CompletionText = keyword,
                    DisplayText = keyword,
                    Snippet = keyword,
                    Kind = wantKind ? "Keyword" : null
                });
            }
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
                response.Description = DocumentationConverter.ConvertDocumentation(symbol.GetDocumentationCommentXml(), _options.FormattingOptions.NewLine);
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
