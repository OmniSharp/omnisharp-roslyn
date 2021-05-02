#nullable enable

using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public class RunTestsInContextTests : AbstractTestFixture
    {
        public RunTestsInContextTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        public override DotNetCliVersion DotNetCliVersion { get; } = DotNetCliVersion.Current;

        protected async Task<RunTestResponse> RunDotNetTestAsync(string projectName, int line, int column, bool shouldPass, string? targetFrameworkVersion = null, bool expectResults = true, bool useRunSettings = false, string fileName = "TestProgram.cs")
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, null, DotNetCliVersion))
            {
                var service = host.GetRequestHandler<RunTestsInContextService>(OmniSharpEndpoints.V2.RunTestsInContext);

                var request = new RunTestsInContextRequest
                {
                    FileName = Path.Combine(testProject.Directory, fileName),
                    Line = line,
                    Column = column,
                    TargetFrameworkVersion = targetFrameworkVersion
                };

                if (useRunSettings)
                {
                    request.RunSettings = Path.Combine(testProject.Directory, "TestRunSettings.runsettings");
                }

                var response = await service.Handle(request);

                if (expectResults)
                {
                    Assert.True(response.Results?.Length > 0, "Expected test to return results.");
                }

                if (shouldPass)
                {
                    Assert.True(response.Pass, "Expected test to pass but it failed");
                    Assert.Null(response.Failure);
                }
                else
                {
                    Assert.False(response.Pass, "Expected test to fail but it passed");
                }

                return response;
            }
        }

        [Theory]
        // In the body
        [InlineData(10, 0, XunitTestProject)]
        [InlineData(10, 20, XunitTestProject)]
        [InlineData(10, 30, XunitTestProject)]
        [InlineData(10, 31, XunitTestProject)]
        [InlineData(11, 8, XunitTestProject)]

        // On the method header
        [InlineData(9, 8, XunitTestProject)]
        [InlineData(9, 15, XunitTestProject)]
        [InlineData(9, 16, XunitTestProject)]
        [InlineData(9, 20, XunitTestProject)]
        [InlineData(9, 24, XunitTestProject)]

        // On the attribute
        [InlineData(8, 8, XunitTestProject)]

        // In the body
        [InlineData(10, 0, NUnitTestProject)]
        [InlineData(10, 20, NUnitTestProject)]
        [InlineData(10, 30, NUnitTestProject)]
        [InlineData(10, 31, NUnitTestProject)]

        // On the method header
        [InlineData(9, 8, NUnitTestProject)]
        [InlineData(9, 15, NUnitTestProject)]
        [InlineData(9, 16, NUnitTestProject)]
        [InlineData(9, 20, NUnitTestProject)]
        [InlineData(9, 24, NUnitTestProject)]

        // On the attribute
        [InlineData(8, 8, NUnitTestProject)]

        // In the body
        [InlineData(13, 0, MSTestProject)]
        [InlineData(13, 20, MSTestProject)]
        [InlineData(13, 30, MSTestProject)]
        [InlineData(13, 31, MSTestProject)]

        // On the method header
        [InlineData(11, 8, MSTestProject)]
        [InlineData(11, 15, MSTestProject)]
        [InlineData(11, 16, MSTestProject)]
        [InlineData(11, 20, MSTestProject)]
        [InlineData(11, 24, MSTestProject)]

        // On the attribute
        [InlineData(10, 8, MSTestProject)]
        public async Task InvokeInTestBody(int line, int column, string projectName)
        {
            var response = await RunDotNetTestAsync(
                projectName,
                line,
                column,
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");

            Assert.Single(response.Results);

            Assert.Equal("passed", response.Results[0].Outcome);
            Assert.Equal("Main.Test.MainTest.Test", response.Results[0].MethodName);
        }

        [Theory]
        [InlineData(18, 24, XunitTestProject, 2)]
        [InlineData(14, 20, XunitTestProject, 2)]
        [InlineData(15, 20, XunitTestProject, 2)]

        [InlineData(17, 26, NUnitTestProject, 2)]
        [InlineData(12, 26, NUnitTestProject, 2)]
        [InlineData(13, 26, NUnitTestProject, 2)]

        [InlineData(21, 28, MSTestProject, 3)]
        [InlineData(17, 17, MSTestProject, 3)]
        [InlineData(18, 17, MSTestProject, 3)]
        public async Task InvokeParameterizedTest(int line, int column, string projectName, int expectedNumTests)
        {
            var response = await RunDotNetTestAsync(
                projectName,
                line,
                column,
                shouldPass: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");

            Assert.Equal(expectedNumTests, response.Results.Length);

            Assert.StartsWith("Main.Test.MainTest.DataDrivenTest1", response.Results[0].MethodName);
            Assert.StartsWith("Main.Test.MainTest.DataDrivenTest1", response.Results[1].MethodName);
        }

        [Theory]
        // Class Header
        [InlineData(5, 4, XunitTestProject, 10)]
        [InlineData(5, 13, XunitTestProject, 10)]
        [InlineData(5, 17, XunitTestProject, 10)]
        [InlineData(6, 0, XunitTestProject, 10)]
        [InlineData(6, 4, XunitTestProject, 10)]

        // Before [Theory]
        [InlineData(7, 7, XunitTestProject, 10)]

        // After closing brace
        [InlineData(11, 9, XunitTestProject, 10)]

        // Between test methods
        [InlineData(12, 0, XunitTestProject, 10)]

        // In non-test method
        [InlineData(31, 0, XunitTestProject, 10)]

        // Nested class
        [InlineData(70, 0, XunitTestProject, 10)]

        // Class Header
        [InlineData(5, 4, NUnitTestProject, 8)]
        [InlineData(5, 13, NUnitTestProject, 8)]
        [InlineData(5, 17, NUnitTestProject, 8)]
        [InlineData(6, 0, NUnitTestProject, 8)]
        [InlineData(6, 4, NUnitTestProject, 8)]

        // Before [Test]
        [InlineData(7, 7, NUnitTestProject, 8)]

        // After closing brace
        [InlineData(11, 9, NUnitTestProject, 8)]

        // Between test methods
        [InlineData(12, 0, NUnitTestProject, 8)]

        // In non-test method
        [InlineData(49, 0, NUnitTestProject, 8)]

        // Nested class
        [InlineData(76, 0, NUnitTestProject, 4)]

        // Class Header
        [InlineData(6, 4, MSTestProject, 10)]
        [InlineData(6, 13, MSTestProject, 10)]
        [InlineData(6, 17, MSTestProject, 10)]
        [InlineData(7, 0, MSTestProject, 10)]
        [InlineData(7, 4, MSTestProject, 10)]

        // Before [TestMethod]
        [InlineData(10, 7, MSTestProject, 10)]

        // After closing brace
        [InlineData(14, 9, MSTestProject, 10)]

        // Between test methods
        [InlineData(15, 0, MSTestProject, 10)]

        // In non-test method
        [InlineData(54, 0, MSTestProject, 10)]

        // In non-test method
        [InlineData(63, 0, MSTestProject, 10)]
        public async Task NoContainingMethodInvokesClass(int line, int column, string projectName, int expectedNumTests)
        {
            var response = await RunDotNetTestAsync(
                projectName,
                line,
                column,
                shouldPass: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");

            Assert.Equal(expectedNumTests, response.Results.Length);
        }

        [Fact]
        public async Task NUnitGenericContainter()
        {
            var response = await RunDotNetTestAsync(
                NUnitTestProject,
                line: 62,
                column: 0,
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");

            Assert.Equal(2, response.Results.Length);

            response = await RunDotNetTestAsync(
                NUnitTestProject,
                64,
                0,
                shouldPass: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");

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
            var response = await RunDotNetTestAsync(
                projectName,
                line,
                column,
                shouldPass: false,
                expectResults: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v3.1");

            Assert.Equal("Could not find any tests to run", response.Failure);
        }
    }
}
