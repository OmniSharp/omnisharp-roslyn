using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models.Highlight;
using OmniSharp.Roslyn.CSharp.Services.Highlighting;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class HighlightFacts : AbstractSingleRequestHandlerTestFixture<HighlightingService>
    {
        public HighlightFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.Highlight;

        [Fact]
        public async Task HighlightSingleLine()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");

            var highlights = await GetHighlightsAsync(testFile, line: 3);

            AssertSyntax(highlights, testFile.Content.Code, 3,
                Keyword("class"),
                ClassName("C1"),
                Punctuation("{"),
                Keyword("int"),
                Field("n"),
                Operator("="),
                Keyword("true"),
                Punctuation(";"),
                Punctuation("}"));
        }

        [Fact]
        public async Task HighlightEntireFile()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");

            var highlights = await GetHighlightsAsync(testFile);

            AssertSyntax(highlights, testFile.Content.Code, 0,
                Keyword("namespace"),
                NamespaceName("N1"),
                Punctuation("{"),
                Keyword("class"),
                ClassName("C1"),
                Punctuation("{"),
                Keyword("int"),
                Field("n"),
                Operator("="),
                Keyword("true"),
                Punctuation(";"),
                Punctuation("}"),
                Punctuation("}")
            );
        }

        [Fact]
        public async Task HighlightStringInterpolation()
        {
            var testFile = new TestFile("a.cs", @"
class C1
{
    string s = $""{5}"";
}
");

            var highlights = await GetHighlightsAsync(testFile);

            AssertSyntax(highlights, testFile.Content.Code, 0,
                Keyword("class"),
                ClassName("C1"),
                Punctuation("{"),
                Keyword("string"),
                Field("s"),
                Operator("="),
                String("$\""),
                Punctuation("{"),
                Number("5"),
                Punctuation("}"),
                String("\""),
                Punctuation(";"),
                Punctuation("}")
            );
        }

        [Fact]
        public async Task HighlightExcludesUnwantedKeywords()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");

            var highlights = await GetHighlightsAsync(testFile, exclude: HighlightClassification.Keyword);

            Assert.DoesNotContain(highlights, x => x.Kind == "keyword");
        }

        [Fact]
        public async Task HighlightExcludesUnwantedPunctuation()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");

            var highlights = await GetHighlightsAsync(testFile, exclude: HighlightClassification.Punctuation);

            Assert.DoesNotContain(highlights, x => x.Kind == "punctuation");
        }

        [Fact]
        public async Task HighlightExcludesUnwantedOperators()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");

            var highlights = await GetHighlightsAsync(testFile, exclude: HighlightClassification.Operator);

            Assert.DoesNotContain(highlights, x => x.Kind == "operator");
        }

        [Fact]
        public async Task HighlightExcludesUnwantedIdentifiers()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");

            var highlights = await GetHighlightsAsync(testFile, exclude: HighlightClassification.Identifier);

            Assert.DoesNotContain(highlights, x => x.Kind == "identifer");
        }

        [Fact]
        public async Task HighlightExcludesUnwantedNames()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");

            var highlights = await GetHighlightsAsync(testFile, exclude: HighlightClassification.Name);

            Assert.DoesNotContain(highlights, x => x.Kind.EndsWith("name"));
        }

        private async Task<HighlightSpan[]> GetHighlightsAsync(TestFile testFile, int? line = null, HighlightClassification? exclude = null)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var request = new HighlightRequest
            {
                FileName = testFile.FileName,
                Lines = line != null ? new[] { line.Value } : null,
                ExcludeClassifications = exclude != null ? new[] { exclude.Value } : null
            };

            var response = await requestHandler.Handle(request);

            return response.Highlights;
        }

        private static void AssertSyntax(HighlightSpan[] highlights, string code, int startLine, params (string kind, string text)[] expectedTokens)
        {
            var lineNo = startLine;
            var lastIndex = 0;
            var lines = SourceText.From(code).Lines;

            for (var i = 0; i < highlights.Length; i++)
            {
                var (kind, text) = expectedTokens[i];
                var highlight = highlights[i];

                string line;
                int start, end;
                do
                {
                    line = lines[lineNo].ToString();
                    start = line.IndexOf(text, lastIndex);
                    if (start == -1)
                    {
                        if (++lineNo >= lines.Count)
                        {
                            throw new Exception($"Could not find token {text} in the code");
                        }

                        lastIndex = 0;
                    }
                }
                while (start == -1);

                end = start + text.Length;
                lastIndex = end;

                Assert.Equal(kind, highlight.Kind);
                Assert.Equal(lineNo, highlight.StartLine);
                Assert.Equal(lineNo, highlight.EndLine);
                Assert.Equal(start, highlight.StartColumn);
                Assert.Equal(end, highlight.EndColumn);
            }

            Assert.Equal(expectedTokens.Length, highlights.Length);
        }

        private static (string kind, string text) ClassName(string text) => ("class name", text);
        private static (string kind, string text) Field(string text) => ("field name", text);
        private static (string kind, string text) Identifier(string text) => ("identifier", text);
        private static (string kind, string text) NamespaceName(string text) => ("namespace name", text);
        private static (string kind, string text) Keyword(string text) => ("keyword", text);
        private static (string kind, string text) Number(string text) => ("number", text);
        private static (string kind, string text) Operator(string text) => ("operator", text);
        private static (string kind, string text) Punctuation(string text) => ("punctuation", text);
        private static (string kind, string text) String(string text) => ("string", text);
    }
}
