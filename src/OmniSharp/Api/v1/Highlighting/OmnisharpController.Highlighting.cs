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
            var results = new List<ClassifiedResult>();

            foreach (var document in documents)
            {
                var project = document.Project.Name;
                var text = await document.GetTextAsync();
                var spans = new List<ClassifiedSpan>();

                if (request.Lines == null || request.Lines.Length == 0)
                {
                    foreach (var span in await Classifier.GetClassifiedSpansAsync(document, new TextSpan(0, text.Length)))
                    {
                        spans.Add(span);
                    }
                }
                else
                {
                    foreach (var line in request.Lines)
                    {
                        foreach (var span in await Classifier.GetClassifiedSpansAsync(document, text.Lines[line - 1].Span))
                        {
                            spans.Add(span);
                        }
                    }
                }

                results.AddRange(FilterSpans(request, spans)
                    .Select(span => new ClassifiedResult()
                    {
                        Span = span,
                        Lines = text.Lines,
                        Project = project
                    }));
            }

            return results
                .GroupBy(z => z.Span.TextSpan.ToString())
                .Select(a => HighlightResponse.FromClassifiedSpan(a.First().Span, a.First().Lines, a.Select(z => z.Project)));
        }

        class ClassifiedResult
        {
            public ClassifiedSpan Span { get; set; }
            public TextLineCollection Lines { get; set; }
            public string Project { get; set; }
        }

        private IEnumerable<ClassifiedSpan> FilterSpans(HighlightRequest request, IEnumerable<ClassifiedSpan> spans)
        {
            if (!request.WantNames)
                spans = spans.Where(x => !x.ClassificationType.EndsWith(" name"));
            if (!request.WantComments)
                spans = spans.Where(x => x.ClassificationType != "comment" && !x.ClassificationType.StartsWith("xml doc comment "));
            if (!request.WantStrings)
                spans = spans.Where(x => x.ClassificationType != "string" && !x.ClassificationType.StartsWith("string "));
            if (!request.WantOperators)
                spans = spans.Where(x => x.ClassificationType != "operator");
            if (!request.WantPunctuation)
                spans = spans.Where(x => x.ClassificationType != "punctuation");
            if (!request.WantKeywords)
                spans = spans.Where(x => x.ClassificationType != "keyword");
            if (!request.WantNumbers)
                spans = spans.Where(x => x.ClassificationType != "number");
            if (!request.WantIdentifiers)
                spans = spans.Where(x => x.ClassificationType != "identifier");
            if (!request.WantPreprocessorKeywords)
                spans = spans.Where(x => x.ClassificationType != "preprocessor keyword");
            if (!request.WantExcludedCode)
                spans = spans.Where(x => x.ClassificationType != "excluded code");

            return spans;
        }
    }
}
