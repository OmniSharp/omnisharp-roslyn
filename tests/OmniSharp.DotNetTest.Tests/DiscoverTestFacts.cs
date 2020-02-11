using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using OmniSharp.DotNetTest.Models;

namespace OmniSharp.DotNetTest.Tests
{
    public class DiscoverTestFacts : AbstractDiscoverTestFacts
    {
        public DiscoverTestFacts(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        public override DotNetCliVersion DotNetCliVersion { get; } = DotNetCliVersion.Current;

        [Fact]
        public async Task DiscoverXUnit()
        {
            await DiscoverTestsAsync(
                XunitTestProject,
                testFramework: "xunit",
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0",
                new Test 
                {
                    FullyQualifiedName = "Main.Test.MainTest.UsesDisplayName",
                    DisplayName = "My Test Name",
                    LineNumber = 37
                });
        }

        [Fact]
        public async Task DiscoverNUnit()
        {
            await DiscoverTestsAsync(
                NUnitTestProject,
                testFramework: "nunit",
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0",
                new Test
                {
                    FullyQualifiedName = "Main.Test.MainTest.Test",
                    DisplayName = "Test",
                    LineNumber = 10
                });
        }

        [Fact]
        public async Task DiscoverMSTest()
        {
            await DiscoverTestsAsync(
                MSTestProject,
                testFramework: "mstest",
                targetFrameworkVersion: ".NETCoreApp,Version=v3.0",
                new Test
                {
                    FullyQualifiedName = "Main.Test.MainTest.CheckStandardOutput",
                    DisplayName = "CheckStandardOutput",
                    LineNumber = 41
                });
        }
    }
}
