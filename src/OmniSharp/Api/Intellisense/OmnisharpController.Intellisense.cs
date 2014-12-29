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
        [HttpPost("autocomplete")]
        public async Task<IActionResult> AutoComplete([FromBody]AutoCompleteRequest request)
        {
            _workspace.EnsureBufferUpdated(request);

            var completions = new List<AutoCompleteResponse>();

            var documents = _workspace.GetDocuments(request.FileName);

            foreach (var document in documents)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var model = await document.GetSemanticModelAsync();
                var symbols = Recommender.GetRecommendedSymbolsAtPosition(model, position, _workspace);
 
                foreach (var symbol in symbols.Where(s => s.Name.ToLower().StartsWith(request.WordToComplete.ToLower())))
                {
                    completions.Add(MakeAutoCompleteResponse(request, symbol));
                    var typeSymbol = symbol as INamedTypeSymbol;
                    if (typeSymbol != null)
                    {
                        foreach (var ctor in typeSymbol.InstanceConstructors)
                        {
                            completions.Add(MakeAutoCompleteResponse(request, ctor));
                        }
                    }
                }
            }
            
            return new ObjectResult(completions);
        }

        private AutoCompleteResponse MakeAutoCompleteResponse(AutoCompleteRequest request, ISymbol symbol)
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
                var methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol != null)
                {
                    response.ReturnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                }
            }

            if (request.WantSnippet)
            {
                response.Snippet = new SnippetGenerator(true).GenerateSnippet(symbol);
            }

            if (request.WantMethodHeader)
            {
                response.MethodHeader = new SnippetGenerator(false).GenerateSnippet(symbol);
            }

            return response;
        }
    }
}