using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using TestUtility;
using Xunit;

namespace OmniSharp.DotNetTest.Tests
{
    public class RunXunitTestFacts : RunTestFactsBase
    {
        public RunXunitTestFacts(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Fact]
        public async Task RunXunitTest()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0");
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData1()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest1",
                testFramework: "xunit",
                shouldPass: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0");
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData2()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest2",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0");
        }

        [Fact]
        public async Task RunXunitTestWithDisplayName()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.UsesDisplayName",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0");
        }

        [Fact]
        public async Task RunXunitTestWithSimilarName()
        {
            var response = await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.TestWithSimilarName",
                testFramework: "xunit",
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0");

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
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0");

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
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0");

            Assert.Single(response.Results);
            Assert.NotEmpty(response.Results[0].StandardOutput);
        }
    }
}
