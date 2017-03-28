using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    /// <summary>
    /// Tests for legacy 'dotnet test' support for project.json support.
    /// </summary>
    public class LegacyRunTestFacts : AbstractSingleRequestHandlerTestFixture<RunDotNetTestService>
    {
        private const string LegacyXunitTestProject = "LegacyXunitTestProject";
        private const string LegacyNunitTestProject = "LegacyNunitTestProject";

        public LegacyRunTestFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmnisharpEndpoints.V2.RunDotNetTest;

        [Fact]
        public async Task RunXunitTest()
        {
            await RunDotNetTestAsync(
                LegacyXunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "xunit",
                shouldPass: true);
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData1()
        {
            await RunDotNetTestAsync(
                LegacyXunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest1",
                testFramework: "xunit",
                shouldPass: false);
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData2()
        {
            await RunDotNetTestAsync(
                LegacyXunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest2",
                testFramework: "xunit",
                shouldPass: true);
        }

        [Fact]
        public async Task RunNunitTest()
        {
            await RunDotNetTestAsync(
                LegacyNunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "nunit",
                shouldPass: true);
        }

        [Fact]
        public async Task RunNunitDataDriveTest1()
        {
            await RunDotNetTestAsync(
                LegacyNunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest1",
                testFramework: "nunit",
                shouldPass: false);
        }

        [Fact]
        public async Task RunNunitDataDriveTest2()
        {
            await RunDotNetTestAsync(
                LegacyNunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest2",
                testFramework: "nunit",
                shouldPass: true);
        }

        [Fact]
        public async Task RunNunitSourceDataDrivenTest()
        {
            await RunDotNetTestAsync(
                LegacyNunitTestProject,
                methodName: "Main.Test.MainTest.SourceDataDrivenTest",
                testFramework: "nunit",
                shouldPass: true);
        }

        private async Task RunDotNetTestAsync(string projectName, string methodName, string testFramework, bool shouldPass)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, useLegacyDotNetCli: true))
            {
                var service = GetRequestHandler(host);

                var request = new RunDotNetTestRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = methodName,
                    TestFrameworkName = testFramework
                };

                var response = await service.Handle(request);

                if (shouldPass)
                {
                    Assert.True(response.Pass, "Expected test to pass but it failed");
                }
                else
                {
                    Assert.False(response.Pass, "Expected test to fail but it passed");
                }
            }
        }
    }
}
