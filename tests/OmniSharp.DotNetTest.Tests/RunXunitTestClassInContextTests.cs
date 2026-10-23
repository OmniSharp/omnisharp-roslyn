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
    public class RunXunitTestClassInContextTests : RunTestsInContextTestBase
    {
        public RunXunitTestClassInContextTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Theory]
        // Class Header
        [InlineData(5, 4, XunitTestProject, 8)]
        [InlineData(5, 13, XunitTestProject, 8)]
        [InlineData(5, 17, XunitTestProject, 8)]
        [InlineData(6, 0, XunitTestProject, 8)]
        [InlineData(6, 4, XunitTestProject, 8)]

        // Before [Theory]
        [InlineData(7, 7, XunitTestProject, 8)]

        // After closing brace
        [InlineData(11, 9, XunitTestProject, 8)]

        // Between test methods
        [InlineData(12, 0, XunitTestProject, 8)]

        // In non-test method
        [InlineData(31, 0, XunitTestProject, 8)]

        // Nested class
        [InlineData(70, 0, XunitTestProject, 8)]

        public async Task NoContainingMethodInvokesClass(int line, int column, string projectName, int expectedNumTests)
        {
            var (methodNames, testFramework) = await GetContextTestMethodNamesAsync(projectName, line, column);

            Assert.Equal(expectedNumTests, methodNames?.Length);
            Assert.Equal(GetTestFramework(projectName), testFramework);
        }
    }
}
