using System.Threading.Tasks;
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

        public override bool UseLegacyDotNetCli { get; } = false;

        [Fact]
        public async Task RunXunitTest()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "xunit",
                shouldPass: true);
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData1()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest1",
                testFramework: "xunit",
                shouldPass: false);
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData2()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest2",
                testFramework: "xunit",
                shouldPass: true);
        }

        [Fact]
        public async Task RunXunitTestWithDisplayName()
        {
            await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.UsesDisplayName",
                testFramework: "xunit",
                shouldPass: true);
        }

        [Fact]
        public async Task RunXunitTestWithSimilarName()
        {
            var response = await RunDotNetTestAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.TestWithSimilarName",
                testFramework: "xunit",
                shouldPass: true);

            Assert.Equal(1, response.Results.Length);
        }

        // NUnit does not work with .NET CLI RTM yet. https://github.com/nunit/dotnet-test-nunit/issues/108
        // When it does, the NUnitTestProject should be updated and the tests below re-enabled.

        //[Fact]
        public async Task RunNunitTest()
        {
            await RunDotNetTestAsync(
                NunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "nunit",
                shouldPass: true);
        }

        //[Fact]
        public async Task RunNunitDataDriveTest1()
        {
            await RunDotNetTestAsync(
                NunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest1",
                testFramework: "nunit",
                shouldPass: false);
        }

        //[Fact]
        public async Task RunNunitDataDriveTest2()
        {
            await RunDotNetTestAsync(
                NunitTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest2",
                testFramework: "nunit",
                shouldPass: true);
        }

        //[Fact]
        public async Task RunNunitSourceDataDrivenTest()
        {
            await RunDotNetTestAsync(
                NunitTestProject,
                methodName: "Main.Test.MainTest.SourceDataDrivenTest",
                testFramework: "nunit",
                shouldPass: true);
        }
    }
}
