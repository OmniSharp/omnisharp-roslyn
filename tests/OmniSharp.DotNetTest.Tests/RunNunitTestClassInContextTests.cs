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
    public class RunNunitTestClassInContextTests : RunTestsInContextTestBase
    {
        public RunNunitTestClassInContextTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Theory]
        // Class Header
        [InlineData(5, 4, NUnitTestProject, 6)]
        [InlineData(5, 13, NUnitTestProject, 6)]
        [InlineData(5, 17, NUnitTestProject, 6)]
        [InlineData(6, 0, NUnitTestProject, 6)]
        [InlineData(6, 4, NUnitTestProject, 6)]

        // Before [Test]
        [InlineData(7, 7, NUnitTestProject, 6)]

        // After closing brace
        [InlineData(11, 9, NUnitTestProject, 6)]

        // Between test methods
        [InlineData(12, 0, NUnitTestProject, 6)]

        // In non-test method
        [InlineData(49, 0, NUnitTestProject, 6)]

        // Nested class
        [InlineData(76, 0, NUnitTestProject, 2)]
        public async Task NoContainingMethodInvokesClass(int line, int column, string projectName, int expectedNumTests)
        {
            var (methodNames, testFramework) = await GetContextTestMethodNamesAsync(projectName, line, column);

            Assert.Equal(expectedNumTests, methodNames?.Length);
            Assert.Equal(GetTestFramework(projectName), testFramework);
        }
    }
}
