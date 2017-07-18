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
                testFramework: "xunit");
        }

        // NUnit does not work with .NET CLI RTM yet. https://github.com/nunit/dotnet-test-nunit/issues/108
        // When it does, the NUnitTestProject should be updated and the tests below re-enabled.

        [Fact]
        public async Task RunNunitTest()
        {
            await GetDotNetTestStartInfoAsync(
                NunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "nunit");
        }

        [Fact]
        public async Task RunMSTestTest()
        {
            await GetDotNetTestStartInfoAsync(
                MSTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "mstest");
        }
    }
}
