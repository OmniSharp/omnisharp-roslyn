using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using TestUtility;
using Xunit;

namespace OmniSharp.DotNetTest.Tests
{
    public class RunMSTestFacts : RunTestFactsBase
    {
        public RunMSTestFacts(ITestOutputHelper testOutput)
            : base(testOutput)
        {
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
