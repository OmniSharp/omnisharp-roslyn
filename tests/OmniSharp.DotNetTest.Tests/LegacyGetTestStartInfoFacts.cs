using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public class LegacyGetTestStartInfoFacts : AbstractSingleRequestHandlerTestFixture<GetDotNetTestStartInfoService>
    {
        private const string LegacyXunitTestProject = "LegacyXunitTestProject";
        private const string LegacyNunitTestProject = "LegacyNunitTestProject";

        public LegacyGetTestStartInfoFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmnisharpEndpoints.V2.GetDotNetTestStartInfo;

        [Fact]
        public async Task RunXunitTest()
        {
            await GetDotNetTestStartInfoAsync(
                LegacyXunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "xunit");
        }

        [Fact]
        public async Task RunNunitTest()
        {
            await GetDotNetTestStartInfoAsync(
                LegacyNunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "nunit");
        }

        private async Task GetDotNetTestStartInfoAsync(string projectName, string methodName, string testFramework)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, useLegacyDotNetCli: true))
            {
                var service = GetRequestHandler(host);

                var request = new GetDotNetTestStartInfoRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = methodName,
                    TestFrameworkName = testFramework
                };

                var response = await service.Handle(request);

                var dotNetCli = host.GetExport<DotNetCliService>();

                Assert.Equal(dotNetCli.DotNetPath, response.Executable);
            }
        }
    }
}
