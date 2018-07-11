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
