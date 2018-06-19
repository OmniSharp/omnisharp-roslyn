using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Cake.Services.RequestHandlers.Navigation;
using OmniSharp.Models.FindUsages;
using OmniSharp.Models.GotoDefinition;
using OmniSharp.Models.UpdateBuffer;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public sealed class FindUsagesFacts : CakeSingleRequestHandlerTestFixture<FindUsagesHandler>
    {
        public FindUsagesFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.FindUsages;

        [Fact]
        public async Task ShouldNotIncludeUsagesFromLoadedFilesWhenOnlyThisFileIsTrue()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");

                await GetUpdateBufferHandler(host).Handle(new UpdateBufferRequest
                {
                    Buffer = "Information(\"Hello World\");",
                    FileName = Path.Combine(testProject.Directory, "foo.cake"),
                    FromDisk = false
                });
                await GetUpdateBufferHandler(host).Handle(new UpdateBufferRequest
                {
                    Buffer = "#load foo.cake\nInformation(\"Hello World\");",
                    FileName = fileName,
                    FromDisk = false
                });

                var request = new FindUsagesRequest
                {
                    FileName = fileName,
                    Line = 1,
                    Column = 1,
                    OnlyThisFile = true
                };

                var requestHandler = GetRequestHandler(host);
                var response = await requestHandler.Handle(request);

                Assert.Single(response.QuickFixes);
            }
        }

        [Fact]
        public async Task ShouldIncludeUsagesFromLoadedFiles()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");
                var loadedFile = Path.Combine(testProject.Directory, "foo.cake");

                await GetUpdateBufferHandler(host).Handle(new UpdateBufferRequest
                {
                    Buffer = "Information(\"Hello World\");",
                    FileName = loadedFile,
                    FromDisk = false
                });
                await GetUpdateBufferHandler(host).Handle(new UpdateBufferRequest
                {
                    Buffer = "#load foo.cake\nInformation(\"Hello World\");",
                    FileName = fileName,
                    FromDisk = false
                });

                var request = new FindUsagesRequest
                {
                    FileName = fileName,
                    Line = 1,
                    Column = 1,
                    OnlyThisFile = false
                };

                var requestHandler = GetRequestHandler(host);
                var response = await requestHandler.Handle(request);

                Assert.Equal(2, response.QuickFixes.Count());
                Assert.Contains(fileName, response.QuickFixes.Select(x => x.FileName));
                Assert.Contains(loadedFile, response.QuickFixes.Select(x => x.FileName));
            }
        }
    }
}
