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
    public class RunMSTestsInContextMethodTests : RunTestsInContextTestBase
    {
        public RunMSTestsInContextMethodTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Theory]
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
            var (methodNames, testFramework) = await GetContextTestMethodNamesAsync(projectName, line, column);

            var methodName = Assert.Single(methodNames ?? throw new Xunit.Sdk.XunitException("Expected a test method."));

            Assert.Equal("Main.Test.MainTest.Test", methodName);
            Assert.Equal(GetTestFramework(projectName), testFramework);
        }
    }
}
