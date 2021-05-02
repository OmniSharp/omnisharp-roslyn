using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public class RunTestFacts : AbstractTestFixture
    {
        public RunTestFacts(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        public override DotNetCliVersion DotNetCliVersion { get; } = DotNetCliVersion.Current;

        protected async Task<RunTestResponse> RunDotNetTestAsync(string projectName, string methodName, string testFramework, bool shouldPass, string targetFrameworkVersion = null, bool expectResults = true, bool useRunSettings = false)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, null, DotNetCliVersion))
            {
                var service = host.GetRequestHandler<RunTestService>(OmniSharpEndpoints.V2.RunTest);

                var request = new RunTestRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = methodName,
                    TestFrameworkName = testFramework,
                    TargetFrameworkVersion = targetFrameworkVersion
                };

                if (useRunSettings)
                {
                    request.RunSettings = Path.Combine(testProject.Directory, "TestRunSettings.runsettings");
                }

                var response = await service.Handle(request);

                if (expectResults)
                {
                    Assert.True(response.Results?.Length > 0, "Expected test to return results.");
                }

                if (shouldPass)
                {
                    Assert.True(response.Pass, "Expected test to pass but it failed");
                }
                else
                {
                    Assert.False(response.Pass, "Expected test to fail but it passed");
                }

                return response;
            }
        }

        [Fact]
        public async Task RunXunitTest()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData1()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest1",
                testFramework: "xunit",
                shouldPass: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData2()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest2",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");
        }

        [Fact]
        public async Task RunXunitTestWithDisplayName()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.UsesDisplayName",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");
        }

        [Fact]
        public async Task RunXunitTestWithSimilarName()
        {
            var response = await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.TestWithSimilarName",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");

            Assert.Single(response.Results);
        }

        [Fact]
        public async Task RunXunitFailingTest()
        {
            var response = await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.FailingTest",
                testFramework: "xunit",
                shouldPass: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");

            Assert.Single(response.Results);
            Assert.NotEmpty(response.Results[0].ErrorMessage);
            Assert.NotEmpty(response.Results[0].ErrorStackTrace);
        }

        [Fact]
        public async Task RunXunitStandardOutputIsReturned()
        {
            var response = await RunDotNetTestAsync(
                NUnitTestProject,
                methodName: "Main.Test.MainTest.CheckStandardOutput",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");

            Assert.Single(response.Results);
            Assert.NotEmpty(response.Results[0].StandardOutput);
        }

        [Fact]
        public async Task RunNunitTest()
        {
            await RunDotNetTestAsync(
                NUnitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "nunit",
                shouldPass: true);
        }

        [Fact]
        public async Task RunNunitDataDriveTest1()
        {
            await RunDotNetTestAsync(
                NUnitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest1",
                testFramework: "nunit",
                shouldPass: false);
        }

        [Fact]
        public async Task RunNunitDataDriveTest2()
        {
            await RunDotNetTestAsync(
                NUnitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest2",
                testFramework: "nunit",
                shouldPass: true);
        }

        [Fact]
        public async Task RunNunitSourceDataDrivenTest()
        {
            await RunDotNetTestAsync(
                NUnitTestProject,
                methodName: "Main.Test.MainTest.SourceDataDrivenTest",
                testFramework: "nunit",
                shouldPass: true);
        }

        [Fact]
        public async Task RunNunitFailingTest()
        {
            var response = await RunDotNetTestAsync(
                NUnitTestProject,
                methodName: "Main.Test.MainTest.FailingTest",
                testFramework: "nunit",
                shouldPass: false);

            Assert.Single(response.Results);
            Assert.NotEmpty(response.Results[0].ErrorMessage);
            Assert.NotEmpty(response.Results[0].ErrorStackTrace);
        }

        [Fact]
        public async Task RunNunitStandardOutputIsReturned()
        {
            var response = await RunDotNetTestAsync(
                NUnitTestProject,
                methodName: "Main.Test.MainTest.CheckStandardOutput",
                testFramework: "nunit",
                shouldPass: true);

            Assert.Single(response.Results);
            Assert.NotEmpty(response.Results[0].StandardOutput);
        }

        [Fact]
        public async Task RunNunitTypedTestRunsTwice()
        {
            var response = await RunDotNetTestAsync(
                NUnitTestProject,
                methodName: "Main.Test.GenericTest`1.TypedTest",
                testFramework: "nunit",
                shouldPass: true);

            Assert.Equal(2, response.Results.Length);
        }

        [Fact]
        public async Task RunMSTestTest()
        {
            await RunDotNetTestAsync(
                MSTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "mstest",
                shouldPass: true);
        }

        [Fact]
        public async Task RunMSTestDataDriveTest1()
        {
            await RunDotNetTestAsync(
                MSTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest1",
                testFramework: "mstest",
                shouldPass: false);
        }

        [Fact]
        public async Task RunMSTestDataDriveTest2()
        {
            await RunDotNetTestAsync(
                MSTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest2",
                testFramework: "mstest",
                shouldPass: true);
        }

        [Fact]
        public async Task RunMSTestFailingTest()
        {
            var response = await RunDotNetTestAsync(
                MSTestProject,
                methodName: "Main.Test.MainTest.FailingTest",
                testFramework: "mstest",
                shouldPass: false);

            Assert.Single(response.Results);
            Assert.NotEmpty(response.Results[0].ErrorMessage);
            Assert.NotEmpty(response.Results[0].ErrorStackTrace);
        }

        [Fact]
        public async Task RunMSTestStandardOutputIsReturned()
        {
            var response = await RunDotNetTestAsync(
                MSTestProject,
                methodName: "Main.Test.MainTest.CheckStandardOutput",
                testFramework: "mstest",
                shouldPass: true);

            Assert.Single(response.Results);
            Assert.NotEmpty(response.Results[0].StandardOutput);
        }

        [Fact]
        public async Task RunMSTestWithRunSettings()
        {
            await RunDotNetTestAsync(
                MSTestProject,
                methodName: "Main.Test.MainTest.CheckRunSettings",
                testFramework: "mstest",
                shouldPass: true,
                useRunSettings: true);
        }

        [Fact]
        public async Task RunMSTestWithoutRunSettings()
        {
            var response = await RunDotNetTestAsync(
                MSTestProject,
                methodName: "Main.Test.MainTest.CheckRunSettings",
                testFramework: "mstest",
                shouldPass: false,
                useRunSettings: false);

            Assert.Single(response.Results);
            Assert.NotEmpty(response.Results[0].ErrorMessage);
            Assert.NotEmpty(response.Results[0].ErrorStackTrace);
        }
    }
}
