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
        [HttpPost("packagesearch")]
        public async Task<IEnumerable<AutoCompleteResponse>> PackageSearch(AutoCompleteRequest request)
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
            //return await Task.FromResult(null);
        }
    }
}
