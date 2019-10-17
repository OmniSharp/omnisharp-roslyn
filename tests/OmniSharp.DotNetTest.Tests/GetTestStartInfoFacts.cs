using OmniSharp.DotNetTest.Models;
using OmniSharp.Services;
using OmniSharp.Utilities;
using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public class GetTestStartInfoFacts : AbstractGetTestStartInfoFacts
    {
        public GetTestStartInfoFacts(ITestOutputHelper output) : base(output)
        {
        }

        public override DotNetCliVersion DotNetCliVersion { get; } = DotNetCliVersion.Current;

        [Fact]
        public async Task RunXunitTest()
        {
            await GetDotNetTestStartInfoAsync(
                XunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "xunit",
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0",
                assert: AssertCorrectness);
        }

        [Fact]
        public async Task RunNunitTest()
        {
            await GetDotNetTestStartInfoAsync(
                NUnitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "nunit",
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0",
                assert: AssertCorrectness);
        }

        [Fact]
        public async Task RunMSTestTest()
        {
            await GetDotNetTestStartInfoAsync(
                MSTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "mstest",
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0",
                assert: AssertCorrectness);
        }

        private static void AssertCorrectness(GetTestStartInfoResponse response, OmniSharpTestHost host)
        {
            // .NET Core 3.0 executable on Windows should be testhost.exe
            if (Platform.Current.OperatingSystem == OperatingSystem.Windows)
            {
                Assert.EndsWith("testhost.exe", response.Executable);
            }
            else // elsewhere, dotnet.exe
            {
                var dotNetCli = host.GetExport<IDotNetCliService>();
                Assert.Equal(dotNetCli.DotNetPath, response.Executable);
            }
        }
    }
}
