using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Lsp.Tests;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Models.SemanticHighlight;
using OmniSharp.Models.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using Range = OmniSharp.Models.V2.Range;
using System.Collections.Generic;
using System.IO;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class OmniSharpSemanticTokensHandlerFacts : AbstractLanguageServerTestBase
    {
        public OmniSharpSemanticTokensHandlerFacts(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ValidateTokenNames()
        {
            var legend = Server.ServerSettings.Capabilities.SemanticTokensProvider.Legend;
            foreach (var tokenType in legend.TokenTypes)
            {
                var kind = tokenType.ToString();
                Assert.True(char.IsLower(kind[0]));
                Assert.DoesNotContain(' ', kind);
            }
        }

        [Fact]
        public async Task InvalidPositionDoesNotThrow()
        {
            var testFile = @"
namespace N1
{
    class C1 { int n = true; }
}
";

            var line = -1;
            var highlights = await GetSemanticHighlightsForLineAsync(testFile, line);

            Assert.Empty(highlights);
        }

        [Fact]
        public async Task SemanticHighlightSingleLine()
        {
            var testFile = @"
namespace N1
{
    class C1 { int n = true; }
}
";

            var line = 3;
            var highlights = await GetSemanticHighlightsForLineAsync(testFile, line);

            AssertSyntax(highlights, testFile, line,
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
            var testFile = @"
namespace N1
{
    class C1 { int n = true; }
}
";

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile, 0,
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
            var testFile = @"
class C1
{
    string s = $""{5}"";
}
";

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile, 0,
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
        public async Task SemanticHighlightWithAsyncEnumerable()
        {
            var testFile = @"
class C1
{
    public async Task C2() {
        string s1 = ""hello"";
        await foreach (var x in e) { }
        string s2 = ""world"";
    }
}";

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile, 0,
                Keyword("class"),
                ClassName("C1"),
                Punctuation("{"),
                Keyword("public"),
                Keyword("async"),
                Identifier("Task"),
                Method("C2"),
                Punctuation("("),
                Punctuation(")"),
                Punctuation("{"),
                Keyword("string"),
                Local("s1"),
                Operator("="),
                String("\"hello\""),
                Punctuation(";"),
                Keyword("await"),
                ControlKeyword("foreach"),
                Punctuation("("),
                Keyword("var"),
                Local("x"),
                ControlKeyword("in"),
                Identifier("e"),
                Punctuation(")"),
                Punctuation("{"),
                Punctuation("}"),
                Keyword("string"),
                Local("s2"),
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
            var testFile = @"
class C1
{
    string s1 = ""hello"";
    int[]? example;
    string s2 = ""world"";
}";

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile, 0,
                Keyword("class"),
                ClassName("C1"),
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
            var testFile = @"
static class C1
{
    static string s = $""{5}"";
}
";

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile, 0,
                Keyword("static"),
                Keyword("class"),
                ClassName("C1", SemanticHighlightModifier.Static),
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
            var testFile = @"
R1 r1 = new R1(string.Empty, 1);
record R1(string S, int I);
";

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile, 0,
                ClassName("R1"),
                Local("r1"),
                Operator("="),
                Keyword("new"),
                ClassName("R1"),
                Punctuation("("),
                Keyword("string"),
                Operator("."),
                Field("Empty", SemanticHighlightModifier.Static),
                Punctuation(","),
                Number("1"),
                Punctuation(")"),
                Punctuation(";"),

                Keyword("record"),
                ClassName("R1"),
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
        public async Task SemanticHighlightRecordStructName()
        {
            var testFile = @"
R1 r1 = new R1(string.Empty, 1);
record struct R1(string S, int I);
";

            var highlights = await GetSemanticHighlightsForFileAsync(testFile);

            AssertSyntax(highlights, testFile, 0,
                StructName("R1"),
                Local("r1"),
                Operator("="),
                Keyword("new"),
                StructName("R1"),
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
                StructName("R1"),
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

        private Task<SemanticHighlightSpan[]> GetSemanticHighlightsForFileAsync(string source)
        {
            return GetSemanticHighlightsAsync(source, range: null);
        }

        private Task<SemanticHighlightSpan[]> GetSemanticHighlightsForLineAsync(string source, int line)
        {
            var range = new Range()
            {
                Start = new Point() { Column = 0, Line = line },
                End = new Point() { Column = 0, Line = line + 1 }
            };

            return GetSemanticHighlightsAsync(source, range);
        }

        private async Task<SemanticHighlightSpan[]> GetSemanticHighlightsAsync(string source, Range range)
        {
            var bufferPath = $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}tokens.cs";
            var testFile = new TestFile(bufferPath, source);

            OmniSharpTestHost.AddFilesToWorkspace(testFile);

            var request = new SemanticTokensParams
            {
                TextDocument = new TextDocumentIdentifier(bufferPath),
            };

            var response = await Client.RequestSemanticTokensFull(request);

            var lastLine = 0;
            var lastStart = 0;

            var spans = new List<SemanticHighlightSpan>();
            for (var index = 0; index < response.Data.Length; index += 5)
            {
                var deltaLine = response.Data[index]; // token line number, relative to the previous token
                var deltaStart = response.Data[index + 1]; // token start character, relative to the previous token(relative to 0 or the previous token’s start if they are on the same line)
                var length = response.Data[index + 2]; // the length of the token.
                var tokenType = response.Data[index + 3]; // will be looked up in SemanticTokensLegend.tokenTypes.We currently ask that tokenType < 65536.
                var tokenModifiers = response.Data[index + 4]; // each set bit will be looked up in SemanticTokensLegend.tokenModifiers

                lastLine += deltaLine;
                if (deltaLine == 0)
                {
                    lastStart += deltaStart;
                }
                else
                {
                    lastStart = deltaStart;
                }

                var span = new SemanticHighlightSpan
                {
                    StartLine = lastLine,
                    StartColumn = lastStart,

                    EndLine = lastLine,
                    EndColumn = lastStart + length,

                    Type = (SemanticHighlightClassification)tokenType,
                    Modifiers = tokenModifiers > 0
                        ? Enum.GetValues(typeof(SemanticHighlightModifier))
                            .Cast<int>()
                            .Where(value => (tokenModifiers & (1 << value)) != 0)
                            .Cast<SemanticHighlightModifier>()
                            .ToArray()
                        : Enumerable.Empty<SemanticHighlightModifier>()
                };

                if (range is null || (range.Contains(span.StartLine, span.StartColumn) && range.Contains(span.EndLine, span.EndColumn)))
                {
                    spans.Add(span);
                }
            }

            return spans.ToArray();
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
