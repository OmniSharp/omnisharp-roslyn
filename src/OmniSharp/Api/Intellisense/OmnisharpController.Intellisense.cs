using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        private HashSet<AutoCompleteResponse> _completions
            = new HashSet<AutoCompleteResponse>();
        
        [HttpPost("autocomplete")]
        public async Task<IActionResult> AutoComplete([FromBody]AutoCompleteRequest request)
        {
            _workspace.EnsureBufferUpdated(request);

            var documents = _workspace.GetDocuments(request.FileName);

            foreach (var document in documents)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var model = await document.GetSemanticModelAsync();
                var symbols = Recommender.GetRecommendedSymbolsAtPosition(model, position, _workspace);
                AddKeywords(model, position);
                
                foreach (var symbol in symbols.Where(s => IsValidCompletion(request.WordToComplete, s.Name)))
                {
                    if (request.WantSnippet)
                    {
                        foreach(var completion in MakeSnippetedResponses(request, symbol))
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

            return new ObjectResult(_completions.OrderBy(c => c.DisplayText));
        }

        private bool IsValidCompletion(string wordToComplete, string suggestion)
        {
            return suggestion.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase);
        }

        private void AddKeywords(SemanticModel model, int position)
        {
            var context = CSharpSyntaxContext.CreateContext(_workspace, model, position);
            var keywordHandler = new KeywordContextHandler();
            var keywords = keywordHandler.Get(context, model, position);

            foreach (var keyword in keywords)
            {
                _completions.Add(new AutoCompleteResponse
                                 {
                                     CompletionText = keyword,
                                     DisplayText = keyword,
                                     Snippet = keyword
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
                foreach (var ctor in typeSymbol.InstanceConstructors)
                {
                    completions.Add(MakeAutoCompleteResponse(request, ctor));
                }
                return completions;
            }
            return new[] { MakeAutoCompleteResponse(request, symbol) };
        }

        private AutoCompleteResponse MakeAutoCompleteResponse(AutoCompleteRequest request, ISymbol symbol, bool includeOptionalParams = true)
        {
            var response = new AutoCompleteResponse();
            response.CompletionText = symbol.Name;

            // TODO: Do something more intelligent here
            response.DisplayText = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            if (request.WantDocumentationForEveryCompletionResult)
            {
                response.Description = symbol.GetDocumentationCommentXml();
            }

            if (request.WantReturnType)
            {
                response.ReturnType = ReturnTypeFormatter.GetReturnType(symbol);
            }

            if (request.WantSnippet)
            {
                var snippetGenerator = new SnippetGenerator();
                snippetGenerator.IncludeMarkers = true;
                snippetGenerator.IncludeOptionalParameters = includeOptionalParams;
                response.Snippet = snippetGenerator.GenerateSnippet(symbol);
            }

            if (request.WantMethodHeader)
            {
                var snippetGenerator = new SnippetGenerator();
                snippetGenerator.IncludeMarkers = false;
                snippetGenerator.IncludeOptionalParameters = includeOptionalParams;
                response.MethodHeader = snippetGenerator.GenerateSnippet(symbol);
            }

            return response;
        }
    }
}