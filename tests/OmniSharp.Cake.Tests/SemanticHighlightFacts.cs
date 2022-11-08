using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Cake.Services.RequestHandlers.SemanticHighlight;
using OmniSharp.Models.SemanticHighlight;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using Range = OmniSharp.Models.V2.Range;

namespace OmniSharp.Cake.Tests
{
    public class SemanticHighlightFacts : CakeSingleRequestHandlerTestFixture<SemanticHighlightHandler>
    {
        public SemanticHighlightFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.Highlight;

        [Fact]
        public async Task SemanticHighlightSingleLine()
        {
            const string source = @"
class C1
{
    class C2 { int n = true; }
}
";

            var line = 3;
            var highlights = await GetSemanticHighlightsForLineAsync(source, line, versionedText: null);

            AssertSyntax(highlights, source, line,
                Keyword("class"),
                ClassName("C2"),
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
            const string source = @"
class C1
{
    class C2 { int n = true; }
}
";

            var highlights = await GetSemanticHighlightsForFileAsync(source);

            AssertSyntax(highlights, source, 0,
                Keyword("class"),
                ClassName("C1"),
                Punctuation("{"),
                Keyword("class"),
                ClassName("C2"),
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

        private Task<SemanticHighlightSpan[]> GetSemanticHighlightsForFileAsync(string source)
        {
            return GetSemanticHighlightsAsync(source, range: null, versionedText: null);
        }

        private Task<SemanticHighlightSpan[]> GetSemanticHighlightsForLineAsync(string source, int line, string versionedText)
        {
            var range = new Range()
            {
                Start = new Point() { Column = 0, Line = line },
                End = new Point() { Column = 0, Line = line + 1 }
            };

            return GetSemanticHighlightsAsync(source, range, versionedText);
        }

        private async Task<SemanticHighlightSpan[]> GetSemanticHighlightsAsync(string source, Range range, string versionedText)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var testFile = new TestFile(Path.Combine(testProject.Directory, "build.cake"), source);

                var request = new SemanticHighlightRequest
                {
                    FileName = testFile.FileName,
                    Range = range,
                    VersionedText = versionedText,
                };

                var updateBufferRequest = new UpdateBufferRequest
                {
                    Buffer = source,
                    FileName = request.FileName,
                    FromDisk = false,
                };

                await GetUpdateBufferHandler(host).Handle(updateBufferRequest);

                var requestHandler = GetRequestHandler(host);

                var response = await requestHandler.Handle(request);

                return response.Spans;
            }
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

        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Method(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.MethodName, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Local(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.LocalName, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) ClassName(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.ClassName, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) StructName(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.StructName, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Field(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.FieldName, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Identifier(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Identifier, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Parameter(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.ParameterName, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) NamespaceName(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.NamespaceName, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Keyword(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Keyword, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) ControlKeyword(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.ControlKeyword, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Number(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.NumericLiteral, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Operator(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Operator, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) Punctuation(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.Punctuation, text, modifiers);
        private static (SemanticHighlightClassification type, string text, SemanticHighlightModifier[] modifiers) String(string text, params SemanticHighlightModifier[] modifiers) => (SemanticHighlightClassification.StringLiteral, text, modifiers);
    }
}
