using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    internal class LegacyGetTestStartInfoFacts : AbstractGetTestStartInfoFacts
    {
        public LegacyGetTestStartInfoFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        public override bool UseLegacyDotNetCli { get; } = true;

        [Fact]
        public async Task RunXunitTest()
        {
            await GetDotNetTestStartInfoAsync(
                LegacyXunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "xunit");
        }

        [Fact]
        public async Task RunNunitTest()
        {
            await GetDotNetTestStartInfoAsync(
                LegacyNunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "nunit");
        }
    }
}
