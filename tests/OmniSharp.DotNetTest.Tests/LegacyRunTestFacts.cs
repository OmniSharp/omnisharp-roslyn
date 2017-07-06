using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    /// <summary>
    /// Tests for legacy 'dotnet test' support for project.json support.
    /// </summary>
    public class LegacyRunTestFacts : AbstractRunTestFacts
    {
        public LegacyRunTestFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        public override DotNetCliVersion DotNetCliVersion { get; } = DotNetCliVersion.Legacy;

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
        
        [Fact]
        public async Task RunMSTest()
        {
            await RunDotNetTestAsync(
                LegacyMSTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "mstest",
                shouldPass: true);
        }

        [Fact]
        public async Task RunMSTestDataDriveTest1()
        {
            await RunDotNetTestAsync(
                LegacyMSTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest1",
                testFramework: "mstest",
                shouldPass: false);
        }

        [Fact]
        public async Task RunMSTestDataDriveTest2()
        {
            await RunDotNetTestAsync(
                LegacyMSTestProject,
                methodName: "Main.Test.MainTest.DataDrivenTest2",
                testFramework: "mstest",
                shouldPass: true);
        }
    }
}
