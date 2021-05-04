using System;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services.Structure;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class BlockStructureFacts : AbstractSingleRequestHandlerTestFixture<BlockStructureService>
    {
        public BlockStructureFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.BlockStructure;

        [Fact]
        public async Task UsesRoslynBlockStructureService()
        {
            var testFile = new TestFile("foo.cs", @"class Foo[|
{
    void M()[|
    {
        if (false)[|
        {
        }|]
    }|]
}|]");
            var text = testFile.Content.Text;

            var lineSpans = (await GetResponseAsync(testFile)).Spans
                .Select(b => b.Range)
                .ToArray();

            var expected = testFile.Content.GetSpans()
                .Select(span => testFile.Content.GetRangeFromSpan(span).ToRange()).ToArray();

            Assert.Equal(expected, lineSpans);
        }

        [Fact]
        public async Task NonExistingFile()
        {
            var request = new BlockStructureRequest
            {
                FileName = $"{Guid.NewGuid().ToString("N")}.cs"
            };

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var response = await requestHandler.Handle(request);

            Assert.NotNull(response);
            Assert.Empty(response.Spans);
        }

        [Fact]
        public async Task SupportsRegionBlocks()
        {
            var testFile = new TestFile("foo.cs", @"
[|#region Code Region Here
class Foo[|
{
    void M()[|
    {
        if (false)[|
        {
        }|]
    }|]
}|]
#endregion|]");

            var regionSpan = Assert.Single((await GetResponseAsync(testFile)).Spans,
                span => span.Kind == CodeFoldingBlockKinds.Region);
            Assert.Equal(1, regionSpan.Range.Start.Line);
            Assert.Equal(0, regionSpan.Range.Start.Column);
            Assert.Equal(11, regionSpan.Range.End.Line);
            Assert.Equal(10, regionSpan.Range.End.Column);
        }

        private Task<BlockStructureResponse> GetResponseAsync(TestFile testFile)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var request = new BlockStructureRequest
            {
                FileName = testFile.FileName,
            };

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            return requestHandler.Handle(request);
        }
    }
}
