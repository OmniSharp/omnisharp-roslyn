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
    public class RunSpecialTestsInContextTests : RunTestsInContextTestBase
    {
        public RunSpecialTestsInContextTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Fact]
        public async Task NUnitGenericContainter()
        {
            var response = await RunDotNetTestAsync(
                NUnitTestProject,
                line: 62,
                column: 0,
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0.0");

            Assert.Equal(2, response.Results.Length);

            response = await RunDotNetTestAsync(
                NUnitTestProject,
                64,
                0,
                shouldPass: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0.0");

            Assert.Equal(4, response.Results.Length);
        }

        [Fact]
        public async Task RunMSTestWithRunSettings()
        {
            await RunDotNetTestAsync(
                MSTestProject,
                line: 50,
                column: 0,
                shouldPass: true,
                useRunSettings: true);
        }

        [Fact]
        public async Task RunMSTestWithoutRunSettings()
        {
            var response = await RunDotNetTestAsync(
                MSTestProject,
                line: 50,
                column: 0,
                shouldPass: false,
                useRunSettings: false);

            Assert.Single(response.Results);
            Assert.NotEmpty(response.Results[0].ErrorMessage);
            Assert.NotEmpty(response.Results[0].ErrorStackTrace);
        }

        [Theory]
        [InlineData(79, 0, XunitTestProject)]
        [InlineData(85, 0, NUnitTestProject)]
        [InlineData(70, 0, MSTestProject)]
        public async Task NoTestsInClass(int line, int column, string projectName)
        {
            var (methodNames, testFramework) = await GetContextTestMethodNamesAsync(projectName, line, column);

            Assert.Null(methodNames);
            Assert.Null(testFramework);
        }
    }
}
