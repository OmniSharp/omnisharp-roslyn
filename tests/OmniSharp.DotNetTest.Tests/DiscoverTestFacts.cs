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
                    FullyQualifiedName = "Main.Test.UsesDisplayName",
                    DisplayName = "My Test Name",
                    Source = "",
                    LineNumber = 36
                });
        }
    }
}
