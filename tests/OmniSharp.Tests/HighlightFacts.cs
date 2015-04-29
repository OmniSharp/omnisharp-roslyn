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
            var regions = (await controller.Highlight(new HighlightRequest() { FileName = "a.cs", Lines = new[] { 2 } })).ToList();

            Assert.Equal(5, regions.Count);
            Assert.Equal(2, regions[0].Line);
            Assert.Equal(2, regions[1].Line);
            Assert.Equal(2, regions[2].Line);
            Assert.Equal(2, regions[3].Line);
            Assert.Equal(2, regions[4].Line);

            Assert.Equal("keyword classname", regions[0].Kind);
            Assert.Equal(0, regions[0].Start);
            Assert.Equal(5, regions[0].End);

            Assert.Equal("classname", regions[1].Kind);
            Assert.Equal(6, regions[1].Start);
            Assert.Equal(8, regions[1].End);

            Assert.Equal("keyword structname", regions[2].Kind);
            Assert.Equal(11, regions[2].Start);
            Assert.Equal(14, regions[2].End);

            Assert.Equal("fieldname", regions[3].Kind);
            Assert.Equal(15, regions[3].Start);
            Assert.Equal(16, regions[3].End);

            Assert.Equal("keyword", regions[4].Kind);
            Assert.Equal(19, regions[4].Start);
            Assert.Equal(23, regions[4].End);
        }

        [Fact]
        public async Task HighlightEntireFile()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", "class C1 { int n = true; }" }
            });

            var controller = new OmnisharpController(workspace, null);
            var regions = (await controller.Highlight(new HighlightRequest() { FileName = "a.cs" })).ToList();

            Assert.Equal(5, regions.Count);
            Assert.Equal(0, regions[0].Line);
            Assert.Equal(0, regions[1].Line);
            Assert.Equal(0, regions[2].Line);
            Assert.Equal(0, regions[3].Line);
            Assert.Equal(0, regions[4].Line);

            Assert.Equal("keyword classname", regions[0].Kind);
            Assert.Equal(0, regions[0].Start);
            Assert.Equal(5, regions[0].End);

            Assert.Equal("classname", regions[1].Kind);
            Assert.Equal(6, regions[1].Start);
            Assert.Equal(8, regions[1].End);

            Assert.Equal("keyword structname", regions[2].Kind);
            Assert.Equal(11, regions[2].Start);
            Assert.Equal(14, regions[2].End);

            Assert.Equal("fieldname", regions[3].Kind);
            Assert.Equal(15, regions[3].Start);
            Assert.Equal(16, regions[3].End);

            Assert.Equal("keyword", regions[4].Kind);
            Assert.Equal(19, regions[4].Start);
            Assert.Equal(23, regions[4].End);
        }
    }
}
