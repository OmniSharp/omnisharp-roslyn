using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public class LegacyGetTestStartInfoFacts : AbstractGetTestStartInfoFacts
    {
        public LegacyGetTestStartInfoFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        public override DotNetCliVersion DotNetCliVersion { get; } = DotNetCliVersion.Legacy;

        [ConditionalFact(typeof(IsLegacyTest))]
        public async Task RunXunitTest()
        {
            await GetDotNetTestStartInfoAsync(
                LegacyXunitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "xunit");
        }

        [ConditionalFact(typeof(IsLegacyTest))]
        public async Task RunNunitTest()
        {
            await GetDotNetTestStartInfoAsync(
                LegacyNUnitTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "nunit");
        }

        [ConditionalFact(typeof(IsLegacyTest))]
        public async Task RunMSTestTest()
        {
            await GetDotNetTestStartInfoAsync(
                LegacyMSTestProject,
                methodName: "Main.Test.MainTest.Test",
                testFramework: "mstest");
        }
    }
}
