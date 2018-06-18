using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Cake.Services.RequestHandlers.Structure;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class BlockStructureFacts : CakeSingleRequestHandlerTestFixture<BlockStructureHandler>
    {
        public BlockStructureFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.BlockStructure;

        [Fact]
        public async Task UsesRoslynBlockStructureService()
        {
            const string code = @"class Foo[|
{
    void M()[|
    {
        if (false)[|
        {
        }|]
    }|]
}|]";
            var (response, testFile) = await GetResponseAsync(code);

            var lineSpans = response.Spans
                .Select(b => b.Range)
                .ToArray();

            var expected = testFile.Content.GetSpans()
                .Select(span => testFile.Content.GetRangeFromSpan(span).ToRange()).ToArray();

            Assert.Equal(expected, lineSpans);
        }

        private async Task<(BlockStructureResponse, TestFile)> GetResponseAsync(string code)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var testFile = new TestFile(Path.Combine(testProject.Directory, "build.cake"), code);
                var requestHandler = GetRequestHandler(host);

                var request = new BlockStructureRequest
                {
                    FileName = testFile.FileName,
                };

                var updateBufferRequest = new UpdateBufferRequest
                {
                    Buffer = testFile.Content.Code,
                    FileName = request.FileName,
                    FromDisk = false
                };

                await GetUpdateBufferHandler(host).Handle(updateBufferRequest);

                var response = await requestHandler.Handle(request);

                return (response, testFile);
            }
        }
    }
}
