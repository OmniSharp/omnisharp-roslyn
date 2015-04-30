using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("highlight")]
        public async Task<IEnumerable<HighlightResponse>> Highlight(HighlightRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var model = await document.GetSemanticModelAsync();
            var text = await document.GetTextAsync();
            
            var results = new List<ClassifiedSpan>();
            if (request.Lines == null || request.Lines.Length == 0)
            {
                results.AddRange(Classifier.GetClassifiedSpans(model, new TextSpan(0, text.Length), _workspace));
            }
            else
            {
                foreach (var line in request.Lines)
                {
                    results.AddRange(Classifier.GetClassifiedSpans(model, text.Lines[line].Span, _workspace));
                }
            }
            
            return results.Select(s => HighlightResponse.FromClassifiedSpan(s, text.Lines));
        }
    }
}
