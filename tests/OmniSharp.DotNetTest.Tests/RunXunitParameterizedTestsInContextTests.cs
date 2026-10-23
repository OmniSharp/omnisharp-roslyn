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
    public class RunXunitParameterizedTestsInContextTests : RunTestsInContextTestBase
    {
        public RunXunitParameterizedTestsInContextTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Theory]
        [InlineData(18, 24, XunitTestProject)]
        [InlineData(14, 20, XunitTestProject)]
        [InlineData(15, 20, XunitTestProject)]

        public async Task InvokeParameterizedTest(int line, int column, string projectName)
        {
            var (methodNames, testFramework) = await GetContextTestMethodNamesAsync(projectName, line, column);

            var methodName = Assert.Single(methodNames ?? throw new Xunit.Sdk.XunitException("Expected a test method."));

            Assert.Equal("Main.Test.MainTest.DataDrivenTest1", methodName);
            Assert.Equal(GetTestFramework(projectName), testFramework);
        }
    }
}
