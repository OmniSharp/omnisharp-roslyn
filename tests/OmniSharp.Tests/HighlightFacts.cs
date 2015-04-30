using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class HighlightFacts
    {
        [Fact]
        public async Task HighlightSingleLine()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", "namespace N1\n{\nclass C1 { int n = true; }\n}" }
            });

            var controller = new OmnisharpController(workspace, null);
            var regions = await controller.Highlight(new HighlightRequest() { FileName = "a.cs", Lines = new[] { 2 } });
            
            ValidateRegions(regions,
                new Region("keyword", 2, 0, 5),
                new Region("class name", 2, 6, 8),
                new Region("punctuation", 2, 9, 10),
                new Region("keyword", 2, 11, 14),
                new Region("identifier", 2, 15, 16),
                new Region("operator", 2, 17, 18),
                new Region("keyword", 2, 19, 23),
                new Region("punctuation", 2, 23, 24),
                new Region("punctuation", 2, 25, 26));
        }

        [Fact]
        public async Task HighlightEntireFile()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", "class C1 { int n = true; }" }
            });

            var controller = new OmnisharpController(workspace, null);
            var regions = await controller.Highlight(new HighlightRequest() { FileName = "a.cs" });

            ValidateRegions(regions,
                new Region("keyword", 0, 0, 5),
                new Region("class name", 0, 6, 8),
                new Region("punctuation", 0, 9, 10),
                new Region("keyword", 0, 11, 14),
                new Region("identifier", 0, 15, 16),
                new Region("operator", 0, 17, 18),
                new Region("keyword", 0, 19, 23),
                new Region("punctuation", 0, 23, 24),
                new Region("punctuation", 0, 25, 26));
        }
        
        [Fact]
        public async Task HighlightStringInterpolation()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", "class C1 { string s = $\"{5}\"; }" }
            });
            
            var controller = new OmnisharpController(workspace, null);
            var regions = await controller.Highlight(new HighlightRequest() { FileName = "a.cs" });
            
            ValidateRegions(regions,
                new Region("keyword", 0, 0, 5),
                new Region("class name", 0, 6, 8),
                new Region("punctuation", 0, 9, 10),
                new Region("keyword", 0, 11, 17),
                new Region("identifier", 0, 18, 19),
                new Region("operator", 0, 20, 21),
                new Region("string", 0, 22, 24),
                new Region("punctuation", 0, 24, 25),
                new Region("number", 0, 25, 26),
                new Region("punctuation", 0, 26, 27),
                new Region("string", 0, 27, 28),
                new Region("punctuation", 0, 28, 29),
                new Region("punctuation", 0, 30, 31));
        }
        
        private void ValidateRegions(IEnumerable<HighlightResponse> regions, params Region[] expected)
        {
            var arr = regions.ToArray();
            Assert.Equal(expected.Length, arr.Length);
            
            for (var i = 0; i < arr.Length; i++) 
            {
                var expect = expected[i];
                var result = arr[i];
                
                Assert.Equal(expect.Kind, result.Kind);
                
                Assert.Equal(expect.StartLine, result.Start.Line);
                Assert.Equal(expect.StartChar, result.Start.Character);
                
                Assert.Equal(expect.EndLine, result.End.Line);
                Assert.Equal(expect.EndChar, result.End.Character);
            }
        }
        
        private class Region
        {
            public int StartLine { get; }
            public int StartChar { get; }
            public int EndLine { get; }
            public int EndChar { get; }
            public string Kind { get; }
            
            public Region(string kind, int line, int start, int end)
            {
                Kind = kind;
                StartLine = EndLine = line;
                StartChar = start;
                EndChar = end;
            }
            
            public Region(string kind, int startLine, int startChar, int endLine, int endChar)
            {
                Kind = kind;
                StartLine = startLine;
                EndLine = endLine;
                StartChar = startChar;
                EndChar = endChar;
            }
        }
    }
}
