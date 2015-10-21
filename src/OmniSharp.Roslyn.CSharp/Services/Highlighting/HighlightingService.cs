using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Highlighting
{
    [OmniSharpHandler(OmnisharpEndpoints.Highlight, LanguageNames.CSharp)]
    public class HighlightingService : RequestHandler<HighlightRequest, HighlightResponse>
    {
        [ImportingConstructor]
        public HighlightingService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<HighlightResponse> Handle(HighlightRequest request)
        {
            var documents = _workspace.GetDocuments(request.FileName);
            if (request.ProjectNames != null && request.ProjectNames.Length > 0)
            {
                documents = documents.Where(d => request.ProjectNames.Contains(d.Project.Name, StringComparer.Ordinal));
            }

            if (request.Classifications == null || request.Classifications.Length > 0)
            {
                request.Classifications = AllClassifications;
            }

            if (request.ExcludeClassifications != null && request.ExcludeClassifications.Length > 0)
            {
                request.Classifications = request.Classifications.Except(request.ExcludeClassifications).ToArray();
            }

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
                    var linesToClassify = request.Lines.Join(
                        text.Lines,
                        line => line - 1,
                        line => line.LineNumber,
                        (requestLine, line) => line.Span);
                    foreach (var lineSpan in linesToClassify)
                    {
                        foreach (var span in await Classifier.GetClassifiedSpansAsync(document, lineSpan))
                        {
                            spans.Add(span);
                        }
                    }
                }

                results.AddRange(FilterSpans(request.Classifications, spans)
                    .Select(span => new ClassifiedResult()
                    {
                        Span = span,
                        Lines = text.Lines,
                        Project = project
                    }));
            }

            return new HighlightResponse()
            {
                Highlights = results
                    .GroupBy(result => result.Span.TextSpan.ToString())
                    .Select(grouping => HighlightSpan.FromClassifiedSpan(grouping.First().Span, grouping.First().Lines, grouping.Select(z => z.Project)))
                    .ToArray()
            };
        }

        class ClassifiedResult
        {
            public ClassifiedSpan Span { get; set; }
            public TextLineCollection Lines { get; set; }
            public string Project { get; set; }
        }

        private HighlightClassification[] AllClassifications = Enum.GetValues(typeof(HighlightClassification)).Cast<HighlightClassification>().ToArray();
        private readonly OmnisharpWorkspace _workspace;

        private IEnumerable<ClassifiedSpan> FilterSpans(HighlightClassification[] classifications, IEnumerable<ClassifiedSpan> spans)
        {
            foreach (var classification in AllClassifications.Except(classifications))
            {
                if (classification == HighlightClassification.Name)
                    spans = spans.Where(x => !x.ClassificationType.EndsWith(" name"));
                else if (classification == HighlightClassification.Comment)
                    spans = spans.Where(x => x.ClassificationType != "comment" && !x.ClassificationType.StartsWith("xml doc comment "));
                else if (classification == HighlightClassification.String)
                    spans = spans.Where(x => x.ClassificationType != "string" && !x.ClassificationType.StartsWith("string "));
                else if (classification == HighlightClassification.PreprocessorKeyword)
                    spans = spans.Where(x => x.ClassificationType != "preprocessor keyword");
                else if (classification == HighlightClassification.ExcludedCode)
                    spans = spans.Where(x => x.ClassificationType != "excluded code");
                else
                    spans = spans.Where(x => x.ClassificationType != Enum.GetName(typeof(HighlightClassification), classification).ToLower());
            }

            return spans;
        }
    }
}
