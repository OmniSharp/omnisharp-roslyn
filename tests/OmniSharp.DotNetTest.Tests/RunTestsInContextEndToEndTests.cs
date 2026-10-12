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
    public class RunTestsInContextEndToEndTests : RunTestsInContextTestBase
    {
        public RunTestsInContextEndToEndTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Fact]
        public async Task RunsXunitMethod()
        {
            var response = await RunDotNetTestAsync(
                XunitTestProject,
                line: 10,
                column: 20,
                shouldPass: true,
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0.0");

            var result = Assert.Single(response.Results);
            Assert.Equal("passed", result.Outcome);
            Assert.Equal("Main.Test.MainTest.Test", result.MethodName);
        }

        [Fact]
        public async Task RunsNunitParameterizedMethod()
        {
            var response = await RunDotNetTestAsync(
                NUnitTestProject,
                line: 12,
                column: 26,
                shouldPass: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0.0");

            Assert.Equal(2, response.Results.Length);
            Assert.All(response.Results, result => Assert.StartsWith("Main.Test.MainTest.DataDrivenTest1", result.MethodName));
        }

        [Fact]
        public async Task RunsMSTestClass()
        {
            var response = await RunDotNetTestAsync(
                MSTestProject,
                line: 6,
                column: 4,
                shouldPass: false,
                targetFrameworkVersion: ".NETCoreApp,Version=v8.0.0");

            Assert.Equal(8, response.Results.Length);
        }
    }
}
