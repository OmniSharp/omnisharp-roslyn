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
    public class XunitTestDiscoveryFacts : TestDiscoveryFactsBase
    {
        public XunitTestDiscoveryFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData(XunitTestProject, TestProgram, 8, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.Test")]
        [InlineData(XunitTestProject, TestProgram, 16, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.DataDrivenTest1")]
        [InlineData(XunitTestProject, TestProgram, 24, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.DataDrivenTest2")]
        [InlineData(XunitTestProject, TestProgram, 53, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.FailingTest")]
        [InlineData(XunitTestProject, TestProgram, 59, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.CheckStandardOutput")]
        [InlineData(XunitTestProject, TestProgram, 29, 21, false, "", "", "")]
        public Task FindTestMethods(string projectName, string fileName, int line, int column, bool expectToFind, string expectedTestFramework, string expectedFeatureName, string expectedMethodName) =>
            AssertTestMethodAsync(projectName, fileName, line, column, expectToFind, expectedTestFramework, expectedFeatureName, expectedMethodName);
    }
}
