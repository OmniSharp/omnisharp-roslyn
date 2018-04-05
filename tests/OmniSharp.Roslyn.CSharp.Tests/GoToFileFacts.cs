using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.GotoFile;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GoToFileFacts : AbstractSingleRequestHandlerTestFixture<GotoFileService>
    {
        public GoToFileFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.GotoFile;

        [Fact]
        public async Task ReturnsAListOfAllWorkspaceFiles()
        {
            var testFile1 = new TestFile("foo.cs", @"class Foo {}");
            var testFile2 = new TestFile("bar.cs", @"class Bar {}");

            var files = await GetFilesAsync(testFile1, testFile2);

            Assert.Equal(2, files.Length);
            Assert.Equal("foo.cs", files[0].FileName);
            Assert.Equal("bar.cs", files[1].FileName);
        }

        [Fact]
        public async Task ReturnsEmptyResponseForEmptyWorskpace()
        {
            var files = await GetFilesAsync();

            Assert.Empty(files);
        }

        private async Task<QuickFix[]> GetFilesAsync(params TestFile[] testFiles)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFiles);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var response = await requestHandler.Handle(new GotoFileRequest());

            return response.QuickFixes.ToArray();
        }
    }
}
