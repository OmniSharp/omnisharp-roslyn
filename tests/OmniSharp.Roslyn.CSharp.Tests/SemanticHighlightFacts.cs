using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models.SemanticHighlight;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services.SemanticHighlight;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class SemanticHighlightFacts : AbstractSingleRequestHandlerTestFixture<SemanticHighlightService>
    {
        public SemanticHighlightFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.SemanticHighlight;

        [Fact]
        public async Task SemanticHighlightSingleLine()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");

            var line = 3;
            var highlights = await GetSemanticHighlightsForLineAsync(testFile, line);

            Console.WriteLine(string.Join("\n", highlights.Select(span => span.Type.ToString())));

            AssertSyntax(highlights, testFile.Content.Code, line,
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
        public async Task SemanticHighlightEntireFile()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

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
        public async Task SemanticHighlightStringInterpolation()
        {
            var testFile = new TestFile("a.cs", @"
class C1
{
    string s = $""{5}"";
}
");

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

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

        private Task<SemanticHighlightSpan[]> GetSemanticHighlightsForFileAsync(TestFile testFile)
        {
            return GetSemanticHighlightsAsync(testFile, range: null);
        }

        private Task<SemanticHighlightSpan[]> GetSemanticHighlightsForLineAsync(TestFile testFile, int line)
        {
            var range = new Range()
            {
                Start = new Point() { Column = 0, Line = line },
                End = new Point() { Column = 0, Line = line + 1 }
            };

            return GetSemanticHighlightsAsync(testFile, range);
        }

        private async Task<SemanticHighlightSpan[]> GetSemanticHighlightsAsync(TestFile testFile, Range range)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var request = new SemanticHighlightRequest
            {
                FileName = testFile.FileName,
                Range = range
            };

            var response = await requestHandler.Handle(request);

            return response.Spans;
        }

        private static void AssertSyntax(SemanticHighlightSpan[] highlights, string code, int startLine, params (SemanticHighlightClassification kind, string text)[] expectedTokens)
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

                Assert.Equal(kind, highlight.Type);
                Assert.Equal(lineNo, highlight.StartLine);
                Assert.Equal(lineNo, highlight.EndLine);
                Assert.Equal(start, highlight.StartColumn);
                Assert.Equal(end, highlight.EndColumn);
            }

            Assert.Equal(expectedTokens.Length, highlights.Length);
        }

        private static (SemanticHighlightClassification type, string text) ClassName(string text) => (SemanticHighlightClassification.ClassName, text);
        private static (SemanticHighlightClassification type, string text) Field(string text) => (SemanticHighlightClassification.FieldName, text);
        private static (SemanticHighlightClassification type, string text) Identifier(string text) => (SemanticHighlightClassification.Identifier, text);
        private static (SemanticHighlightClassification type, string text) NamespaceName(string text) => (SemanticHighlightClassification.NamespaceName, text);
        private static (SemanticHighlightClassification type, string text) Keyword(string text) => (SemanticHighlightClassification.Keyword, text);
        private static (SemanticHighlightClassification type, string text) Number(string text) => (SemanticHighlightClassification.NumericLiteral, text);
        private static (SemanticHighlightClassification type, string text) Operator(string text) => (SemanticHighlightClassification.Operator, text);
        private static (SemanticHighlightClassification type, string text) Punctuation(string text) => (SemanticHighlightClassification.Punctuation, text);
        private static (SemanticHighlightClassification type, string text) String(string text) => (SemanticHighlightClassification.StringLiteral, text);
    }
}
