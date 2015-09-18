using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Highlighting;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class HighlightFacts
    {
        [Fact]
        public async Task HighlightSingleLine()
        {
            var code = @"
                namespace N1
                {
                    class C1 { int n = true; }
                }
            ";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", code }
            });

            var controller = new HighlightingService(workspace, new FakeLoggerFactory());
            var regions = await controller.Handle(new HighlightRequest() { FileName = "a.cs", Lines = new[] { 4 } });

            AssertSyntax(regions.Highlights, code, 3,
                Token("class", "keyword"),
                Token("C1", "class name"),
                Token("{", "punctuation"),
                Token("int", "keyword"),
                Token("n", "identifier"),
                Token("=", "operator"),
                Token("true", "keyword"),
                Token(";", "punctuation"),
                Token("}", "punctuation"));
        }

        [Fact]
        public async Task HighlightEntireFile()
        {
            var code = @"
                namespace N1
                {
                    class C1 { int n = true; }
                }
            ";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", code }
            });

            var controller = new HighlightingService(workspace, new FakeLoggerFactory());
            var regions = await controller.Handle(new HighlightRequest() { FileName = "a.cs" });

            AssertSyntax(regions.Highlights, code, 0,
                Token("namespace", "keyword"),
                Token("N1", "identifier"),
                Token("{", "punctuation"),
                Token("class", "keyword"),
                Token("C1", "class name"),
                Token("{", "punctuation"),
                Token("int", "keyword"),
                Token("n", "identifier"),
                Token("=", "operator"),
                Token("true", "keyword"),
                Token(";", "punctuation"),
                Token("}", "punctuation"),
                Token("}", "punctuation"));
        }

        [Fact]
        public async Task HighlightStringInterpolation()
        {
            var code = @"
                class C1
                {
                    string s = $""{5}"";
                }
            ";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", code }
            });

            var controller = new HighlightingService(workspace, new FakeLoggerFactory());
            var regions = await controller.Handle(new HighlightRequest() { FileName = "a.cs" });

            AssertSyntax(regions.Highlights, code, 0,
                Token("class", "keyword"),
                Token("C1", "class name"),
                Token("{", "punctuation"),
                Token("string", "keyword"),
                Token("s", "identifier"),
                Token("=", "operator"),
                Token("$\"", "string"),
                Token("{", "punctuation"),
                Token("5", "number"),
                Token("}", "punctuation"),
                Token("\"", "string"),
                Token(";", "punctuation"),
                Token("}", "punctuation"));
        }

        private void AssertSyntax(IEnumerable<HighlightSpan> regions, string code, int startLine, params TokenSpec[] expected)
        {
            var arr = regions.ToArray();

            var lineNo = startLine;
            var lastIndex = 0;
            var lines = Microsoft.CodeAnalysis.Text.SourceText.From(code).Lines;
            for (var i = 0; i < arr.Length; i++)
            {
                var tokenSpec = expected[i];
                var region = arr[i];
                string line;
                int start, end;
                do {
                    line = lines[lineNo].ToString();
                    start = line.IndexOf(tokenSpec.Text, lastIndex);
                    if (start == -1)
                    {
                        if(++lineNo >= lines.Count)
                        {
                            throw new Exception($"Could not find token {tokenSpec.Text} in the code");
                        }

                        lastIndex = 0;
                    }
                } while (start == -1);
                end = start + tokenSpec.Text.Length;
                lastIndex = end;

                Assert.Equal(tokenSpec.Kind, region.Kind);
                Assert.Equal(lineNo, region.StartLine - 1);
                Assert.Equal(lineNo, region.EndLine - 1);
                Assert.Equal(start, region.StartColumn - 1);
                Assert.Equal(end, region.EndColumn - 1);
            }
            Assert.Equal(expected.Length, arr.Length);
        }

        [Fact]
        public async Task HighlightExcludesUnwantedKeywords()
        {
            var code = @"
                namespace N1
                {
                    class C1 { int n = true; }
                }
            ";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", code }
            });

            var controller = new HighlightingService(workspace, new FakeLoggerFactory());
            var regions = await controller.Handle(new HighlightRequest() { FileName = "a.cs", ExcludeClassifications = new [] { HighlightClassification.Keyword } });

            Assert.DoesNotContain(regions.Highlights, x => x.Kind == "keyword");
        }

        [Fact]
        public async Task HighlightExcludesUnwantedPunctuation()
        {
            var code = @"
                namespace N1
                {
                    class C1 { int n = true; }
                }
            ";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", code }
            });

            var controller = new HighlightingService(workspace, new FakeLoggerFactory());
            var regions = await controller.Handle(new HighlightRequest() { FileName = "a.cs", ExcludeClassifications = new [] { HighlightClassification.Punctuation } });

            Assert.DoesNotContain(regions.Highlights, x => x.Kind == "punctuation");
        }

        [Fact]
        public async Task HighlightExcludesUnwantedOperators()
        {
            var code = @"
                namespace N1
                {
                    class C1 { int n = true; }
                }
            ";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", code }
            });

            var controller = new HighlightingService(workspace, new FakeLoggerFactory());
            var regions = await controller.Handle(new HighlightRequest() { FileName = "a.cs", ExcludeClassifications = new [] { HighlightClassification.Operator } });

            Assert.DoesNotContain(regions.Highlights, x => x.Kind == "operator");
        }

        [Fact]
        public async Task HighlightExcludesUnwantedIdentifiers()
        {
            var code = @"
                namespace N1
                {
                    class C1 { int n = true; }
                }
            ";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", code }
            });

            var controller = new HighlightingService(workspace, new FakeLoggerFactory());
            var regions = await controller.Handle(new HighlightRequest() { FileName = "a.cs", ExcludeClassifications = new [] { HighlightClassification.Identifier } });

            Assert.DoesNotContain(regions.Highlights, x => x.Kind == "identifier");
        }

        [Fact]
        public async Task HighlightExcludesUnwantedNames()
        {
            var code = @"
                namespace N1
                {
                    class C1 { int n = true; }
                }
            ";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", code }
            });

            var controller = new HighlightingService(workspace, new FakeLoggerFactory());
            var regions = await controller.Handle(new HighlightRequest() { FileName = "a.cs", ExcludeClassifications = new [] { HighlightClassification.Name } });

            Assert.DoesNotContain(regions.Highlights, x => x.Kind.EndsWith(" name"));
        }

        private TokenSpec Token(string text, string kind)
        {
            return new TokenSpec(kind, text);
        }
        private class TokenSpec
        {
            public string Text { get; }
            public string Kind { get; }

            public TokenSpec(string kind, string text)
            {
                Kind = kind;
                Text = text;
            }
        }
    }
}
