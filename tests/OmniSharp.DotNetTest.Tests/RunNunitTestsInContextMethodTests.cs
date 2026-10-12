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
    public class RunNunitTestsInContextMethodTests : RunTestsInContextTestBase
    {
        public RunNunitTestsInContextMethodTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Theory]
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
        public async Task InvokeInTestBody(int line, int column, string projectName)
        {
            var (methodNames, testFramework) = await GetContextTestMethodNamesAsync(projectName, line, column);

            var methodName = Assert.Single(methodNames ?? throw new Xunit.Sdk.XunitException("Expected a test method."));

            Assert.Equal("Main.Test.MainTest.Test", methodName);
            Assert.Equal(GetTestFramework(projectName), testFramework);
        }
    }
}
