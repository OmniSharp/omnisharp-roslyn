#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using OmniSharp.Eventing;
using OmniSharp.Services;
using TestUtility;
using Xunit;

namespace OmniSharp.DotNetTest.Tests
{
    public class RunMSTestClassInContextTests : RunTestsInContextTestBase
    {
        public RunMSTestClassInContextTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Theory]
        // Class Header
        [InlineData(6, 4, MSTestProject, 6)]
        [InlineData(6, 13, MSTestProject, 6)]
        [InlineData(6, 17, MSTestProject, 6)]
        [InlineData(7, 0, MSTestProject, 6)]
        [InlineData(7, 4, MSTestProject, 6)]

        // Before [TestMethod]
        [InlineData(10, 7, MSTestProject, 6)]

        // After closing brace
        [InlineData(14, 9, MSTestProject, 6)]

        // Between test methods
        [InlineData(15, 0, MSTestProject, 6)]

        // In non-test method
        [InlineData(54, 0, MSTestProject, 6)]

        // In non-test method
        [InlineData(63, 0, MSTestProject, 6)]
        public async Task NoContainingMethodInvokesClass(int line, int column, string projectName, int expectedNumTests)
        {
            var (methodNames, testFramework) = await GetContextTestMethodNamesAsync(projectName, line, column);

            Assert.Equal(expectedNumTests, methodNames?.Length);
            Assert.Equal(GetTestFramework(projectName), testFramework);
        }
    }
}
