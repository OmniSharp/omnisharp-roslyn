using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public abstract class AbstractRunTestFacts : AbstractSingleRequestHandlerTestFixture<RunTestService>
    {
        protected const string LegacyXunitTestProject = "LegacyXunitTestProject";
        protected const string LegacyNunitTestProject = "LegacyNunitTestProject";
        protected const string XunitTestProject = "XunitTestProject";
        protected const string NunitTestProject = "NunitTestProject";

        public AbstractRunTestFacts(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        protected override string EndpointName { get; } = OmnisharpEndpoints.V2.RunTest;

        public abstract bool UseLegacyDotNetCli { get; }

        protected async Task RunDotNetTestAsync(string projectName, string methodName, string testFramework, bool shouldPass)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, useLegacyDotNetCli: UseLegacyDotNetCli))
            {
                var service = GetRequestHandler(host);

                var request = new RunTestRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = methodName,
                    TestFrameworkName = testFramework
                };

                var response = await service.Handle(request);

                if (shouldPass)
                {
                    Assert.True(response.Pass, "Expected test to pass but it failed");
                }
                else
                {
                    Assert.False(response.Pass, "Expected test to fail but it passed");
                }
            }
        }
    }
}
