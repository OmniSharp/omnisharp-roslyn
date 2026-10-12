using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using TestUtility;
using Xunit;

namespace OmniSharp.DotNetTest.Tests
{
    public class RunNunitTestFacts : RunTestFactsBase
    {
        public RunNunitTestFacts(ITestOutputHelper testOutput)
            : base(testOutput)
        {
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
        public async Task RunNunitTheoryTest()
        {
            var response = await RunDotNetTestAsync(
                NUnitTestProject,
                methodName: "Main.Test.TheorySampleTestsGeneric`1.TestGenericForArbitraryArray",
                testFramework: "nunit",
                shouldPass: true);

            Assert.Single(response.Results);
        }
    }
}
