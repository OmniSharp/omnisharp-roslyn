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
    public class RunXunitTestsInContextMethodTests : RunTestsInContextTestBase
    {
        public RunXunitTestsInContextMethodTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
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

        public async Task InvokeInTestBody(int line, int column, string projectName)
        {
            var (methodNames, testFramework) = await GetContextTestMethodNamesAsync(projectName, line, column);

            var methodName = Assert.Single(methodNames ?? throw new Xunit.Sdk.XunitException("Expected a test method."));

            Assert.Equal("Main.Test.MainTest.Test", methodName);
            Assert.Equal(GetTestFramework(projectName), testFramework);
        }
    }
}
