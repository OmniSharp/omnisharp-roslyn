using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public class RunTestFacts : AbstractRunTestFacts
    {
        public RunTestFacts(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        public override DotNetCliVersion DotNetCliVersion { get; } = DotNetCliVersion.Current;

        [Fact]
        public async Task RunXunitTest()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0");
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData1()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest1",
                testFramework: "xunit",
                shouldPass: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0");
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData2()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest2",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0");
        }

        [Fact]
        public async Task RunXunitTestWithDisplayName()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.UsesDisplayName",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0");
        }

        [Fact]
        public async Task RunXunitTestWithSimilarName()
        {
            var response = await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.TestWithSimilarName",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0");

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
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0");

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
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0");

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
    }
}
