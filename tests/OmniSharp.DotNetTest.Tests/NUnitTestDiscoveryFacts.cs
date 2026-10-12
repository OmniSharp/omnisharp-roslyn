using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.MembersTree;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeStructure;
using OmniSharp.Roslyn.CSharp.Services.Structure;
using TestUtility;
using Xunit;

namespace OmniSharp.DotNetTest.Tests
{
    public class NUnitTestDiscoveryFacts : TestDiscoveryFactsBase
    {
        public NUnitTestDiscoveryFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData(NUnitTestProject, TestProgram, 8, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.Test")]
        [InlineData(NUnitTestProject, TestProgram, 15, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.DataDrivenTest1")]
        [InlineData(NUnitTestProject, TestProgram, 22, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.DataDrivenTest2")]
        [InlineData(NUnitTestProject, TestProgram, 28, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.SourceDataDrivenTest")]
        [InlineData(NUnitTestProject, TestProgram, 34, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.FailingTest")]
        [InlineData(NUnitTestProject, TestProgram, 47, 20, false, "", "", "")]
        [InlineData(NUnitTestProject, TestProgram, 60, 20, true, nunit, NUnitTestMethod, "Main.Test.GenericTest`1.TypedTest")]
        public Task FindTestMethods(string projectName, string fileName, int line, int column, bool expectToFind, string expectedTestFramework, string expectedFeatureName, string expectedMethodName) =>
            AssertTestMethodAsync(projectName, fileName, line, column, expectToFind, expectedTestFramework, expectedFeatureName, expectedMethodName);
    }
}
