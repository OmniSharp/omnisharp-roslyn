using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public abstract class AbstractRunTestFacts : AbstractTestFixture
    {
        protected const string LegacyXunitTestProject = "LegacyXunitTestProject";
        protected const string LegacyNunitTestProject = "LegacyNunitTestProject";
        protected const string LegacyMSTestProject = "LegacyMSTestProject";
        protected const string XunitTestProject = "XunitTestProject";
        protected const string NUnitTestProject = "NUnitTestProject";
        protected const string MSTestProject = "MSTestProject";

        public AbstractRunTestFacts(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        internal RunTestService GetRequestHandler(OmniSharpTestHost host)
        {
            return host.GetRequestHandler<RunTestService>(OmniSharpEndpoints.V2.RunTest);
        }

        public abstract DotNetCliVersion DotNetCliVersion { get; }

        protected async Task<RunTestResponse> RunDotNetTestAsync(string projectName, string methodName, string testFramework, bool shouldPass, bool expectResults = true)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, dotNetCliVersion: DotNetCliVersion))
            {
                var service = GetRequestHandler(host);

                var request = new RunTestRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = methodName,
                    TestFrameworkName = testFramework
                };

                var response = await service.Handle(request);

                if (expectResults)
                {
                    Assert.True(response.Results?.Length > 0, "Expected test to return results.");
                }

                if (shouldPass)
                {
                    Assert.True(response.Pass, "Expected test to pass but it failed");
                }
                else
                {
                    Assert.False(response.Pass, "Expected test to fail but it passed");
                }

                return response;
            }
        }
    }
}
