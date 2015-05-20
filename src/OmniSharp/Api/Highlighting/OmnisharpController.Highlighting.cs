using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
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
            var documents = _workspace.GetDocuments(request.FileName);
            var results = new List<Tuple<ClassifiedSpan, string, TextLineCollection>>();

            foreach (var document in documents) {
                var spans = await this.GetHighlightSpan(request.Lines, document);
                results.AddRange(spans);
            }

            return results.GroupBy(z => z.Item1.TextSpan.ToString()).Select(a => HighlightResponse.FromClassifiedSpan(a.First().Item1, a.First().Item3, a.Select(z => z.Item2)));
        }

        private async Task<IEnumerable<Tuple<ClassifiedSpan, string, TextLineCollection>>> GetHighlightSpan(int[] Lines, Document document)
        {
            var results = new List<Tuple<ClassifiedSpan, string, TextLineCollection>>();
            var project = document.Project.Name;
            var model = await document.GetSemanticModelAsync();
            var text = await document.GetTextAsync();

            if (Lines == null || Lines.Length == 0)
            {
                foreach (var span in Classifier.GetClassifiedSpans(model, new TextSpan(0, text.Length), _workspace))
                    results.Add(Tuple.Create(span, project, text.Lines));
            }
            else
            {
                foreach (var line in Lines)
                {
                    foreach (var span in Classifier.GetClassifiedSpans(model, text.Lines[line].Span, _workspace))
                        results.Add(Tuple.Create(span, project, text.Lines));
                }
            }
            return results;
        }
    }
}
