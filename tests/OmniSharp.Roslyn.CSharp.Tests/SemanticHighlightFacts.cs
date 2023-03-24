using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models.SemanticHighlight;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services.SemanticHighlight;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using Range = OmniSharp.Models.V2.Range;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class SemanticHighlightFacts : AbstractSingleRequestHandlerTestFixture<SemanticHighlightService>
    {
        public SemanticHighlightFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.Highlight;

        [Fact]
        public async Task InvalidPositionDoesNotThrow()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");

            var line = -1;
            var highlights = await GetSemanticHighlightsForLineAsync(testFile, line, versionedText: null);

            Assert.Empty(highlights);
        }

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
            var highlights = await GetSemanticHighlightsForLineAsync(testFile, line, versionedText: null);

            AssertSyntax(highlights, testFile.Content.Code, line,
                Keyword("class"),
                Class("C1"),
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
                Namespace("N1"),
                Punctuation("{"),
                Keyword("class"),
                Class("C1"),
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
        public async Task SemanticHighlightEntireFileWithVersionedText()
        {
            var testFile = new TestFile("a.cs", @"
namespace N1
{
    class C1 { int n = true; }
}
");
            var versionedText = @"
namespace N1
{
    class C { int n = false; }
}
";

            var highlights = await GetSemanticHighlightsForFileAsync(testFile, versionedText);

            AssertSyntax(highlights, versionedText, 0,
                Keyword("namespace"),
                Namespace("N1"),
                Punctuation("{"),
                Keyword("class"),
                Class("C"),
                Punctuation("{"),
                Keyword("int"),
                Field("n"),
                Operator("="),
                Keyword("false"),
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
                Class("C1"),
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
        public async Task SemanticHighlightWithAsyncEnumerable()
        {
            var testFile = new TestFile("a.cs", @"
class C1
{
    public async Task C2() {
        string s1 = ""hello"";
        await foreach (var x in e) { }
        string s2 = ""world"";
    }
}");

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile.Content.Code, 0,
                Keyword("class"),
                Class("C1"),
                Punctuation("{"),
                Keyword("public"),
                Keyword("async"),
                Identifier("Task"),
                Method("C2"),
                Punctuation("("),
                Punctuation(")"),
                Punctuation("{"),
                Keyword("string"),
                Variable("s1"),
                Operator("="),
                String("\"hello\""),
                Punctuation(";"),
                Keyword("await"),
                ControlKeyword("foreach"),
                Punctuation("("),
                Keyword("var"),
                Variable("x"),
                ControlKeyword("in"),
                Identifier("e"),
                Punctuation(")"),
                Punctuation("{"),
                Punctuation("}"),
                Keyword("string"),
                Variable("s2"),
                Operator("="),
                String("\"world\""),
                Punctuation(";"),
                Punctuation("}"),
                Punctuation("}")
            );
        }

        [Fact]
        public async Task SemanticHighlightWithNullable()
        {
            var testFile = new TestFile("a.cs", @"
class C1
{
    string s1 = ""hello"";
    int[]? example;
    string s2 = ""world"";
}");

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile.Content.Code, 0,
                Keyword("class"),
                Class("C1"),
                Punctuation("{"),
                Keyword("string"),
                Field("s1"),
                Operator("="),
                String("\"hello\""),
                Punctuation(";"),
                Keyword("int"),
                Punctuation("["),
                Punctuation("]"),
                Operator("?"),
                Field("example"),
                Punctuation(";"),
                Keyword("string"),
                Field("s2"),
                Operator("="),
                String("\"world\""),
                Punctuation(";"),
                Punctuation("}")
            );
        }

        [Fact]
        public async Task SemanticHighlightStaticModifiers()
        {
            var testFile = new TestFile("a.cs", @"
static class C1
{
    static string s = $""{5}"";
}
");

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile.Content.Code, 0,
                Keyword("static"),
                Keyword("class"),
                Class("C1", SemanticHighlightModifier.Static),
                Punctuation("{"),
                Keyword("static"),
                Keyword("string"),
                Field("s", SemanticHighlightModifier.Static),
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
        public async Task SemanticHighlightRecordName()
        {
            var testFile = new TestFile("a.cs", @"
R1 r1 = new R1(string.Empty, 1);
record R1(string S, int I);
");

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile.Content.Code, 0,
                Class("R1"),
                Variable("r1"),
                Operator("="),
                Keyword("new"),
                Class("R1"),
                Punctuation("("),
                Keyword("string"),
                Operator("."),
                Field("Empty", SemanticHighlightModifier.Static),
                Punctuation(","),
                Number("1"),
                Punctuation(")"),
                Punctuation(";"),

                Keyword("record"),
                Class("R1"),
                Punctuation("("),
                Keyword("string"),
                Parameter("S"),
                Punctuation(","),
                Keyword("int"),
                Parameter("I"),
                Punctuation(")"),
                Punctuation(";")
            );
        }

        [Fact]
        public async Task SemanticHighlightRecordStruct()
        {
            var testFile = new TestFile("a.cs", @"
R1 r1 = new R1(string.Empty, 1);
record struct R1(string S, int I);
");

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile.Content.Code, 0,
                Struct("R1"),
                Variable("r1"),
                Operator("="),
                Keyword("new"),
                Struct("R1"),
                Punctuation("("),
                Keyword("string"),
                Operator("."),
                Field("Empty", SemanticHighlightModifier.Static),
                Punctuation(","),
                Number("1"),
                Punctuation(")"),
                Punctuation(";"),

                Keyword("record"),
                Keyword("struct"),
                Struct("R1"),
                Punctuation("("),
                Keyword("string"),
                Parameter("S"),
                Punctuation(","),
                Keyword("int"),
                Parameter("I"),
                Punctuation(")"),
                Punctuation(";")
            );
        }

        [Fact]
        public async Task SemanticHighlightLinkedFiles()
        {
            var testFile = new TestFile("a.cs", @"
class C1 { }
");

            TestHelpers.AddProjectToWorkspace(
                SharedOmniSharpTestHost.Workspace,
                Path.Combine(Directory.GetCurrentDirectory(), "a.csproj"),
                new[] { "net472" },
                new[] { testFile });

            TestHelpers.AddProjectToWorkspace(
                SharedOmniSharpTestHost.Workspace,
                Path.Combine(Directory.GetCurrentDirectory(), "b.csproj"),
                new[] { "net472" },
                new[] { testFile });

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var request = new SemanticHighlightRequest
            {
                FileName = "a.cs",
            };

            var response = await requestHandler.Handle(request);

            AssertSyntax(response.Spans, testFile.Content.Code, 0,
                Keyword("class"),
                Class("C1"),
                Punctuation("{"),
                Punctuation("}")
            );
        }

        private Task<SemanticHighlightSpan[]> GetSemanticHighlightsForFileAsync(TestFile testFile)
        {
            return GetSemanticHighlightsAsync(testFile, range: null, versionedText: null);
        }

        private Task<SemanticHighlightSpan[]> GetSemanticHighlightsForFileAsync(TestFile testFile, string versionedText)
        {
            return GetSemanticHighlightsAsync(testFile, range: null, versionedText);
        }

        private Task<SemanticHighlightSpan[]> GetSemanticHighlightsForLineAsync(TestFile testFile, int line, string versionedText)
        {
            var range = new Range()
            {
                Start = new Point() { Column = 0, Line = line },
                End = new Point() { Column = 0, Line = line + 1 }
            };

            return GetSemanticHighlightsAsync(testFile, range, versionedText);
        }

        private async Task<SemanticHighlightSpan[]> GetSemanticHighlightsAsync(TestFile testFile, Range range, string versionedText)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var request = new SemanticHighlightRequest
            {
                FileName = testFile.FileName,
                Range = range,
                VersionedText = versionedText,
            };

            var response = await requestHandler.Handle(request);

            return response.Spans;
        }

        private static void AssertSyntax(SemanticHighlightSpan[] highlights, string code, int startLine, params (SemanticHighlightClassification kind, string text, SemanticHighlightModifier[] modifiers)[] expectedTokens)
        {
            var lineNo = startLine;
            var lastIndex = 0;
            var lines = SourceText.From(code).Lines;

            for (var i = 0; i < highlights.Length; i++)
            {
                var (type, text, modifiers) = expectedTokens[i];
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

                Assert.Equal(type, highlight.Type);
                Assert.Equal(modifiers, highlight.Modifiers);
                Assert.Equal(lineNo, highlight.StartLine);
                Assert.Equal(lineNo, highlight.EndLine);
                Assert.Equal(start, highlight.StartColumn);
                Assert.Equal(end, highlight.EndColumn);
            }

            Assert.Equal(expectedTokens.Length, highlights.Length);
        }

        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Method(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Method, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Variable(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Variable, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Class(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Class, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Struct(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Struct, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Field(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Field, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Identifier(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Identifier, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Parameter(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Parameter, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Namespace(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Namespace, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Keyword(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Keyword, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) ControlKeyword(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.ControlKeyword, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Number(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.NumericLiteral, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Operator(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Operator, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Punctuation(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Punctuation, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) String(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.StringLiteral, text, modifiers);
    }
}
