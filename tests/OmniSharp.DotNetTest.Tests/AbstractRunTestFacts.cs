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
        public AbstractRunTestFacts(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        internal RunTestService GetRequestHandler(OmniSharpTestHost host)
        {
            return host.GetRequestHandler<RunTestService>(OmniSharpEndpoints.V2.RunTest);
        }

        protected async Task<RunTestResponse> RunDotNetTestAsync(string projectName, string methodName, string testFramework, bool shouldPass, string targetFrameworkVersion = null, bool expectResults = true, bool useRunSettings = false)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, null, DotNetCliVersion))
            {
                var service = GetRequestHandler(host);

                var request = new RunTestRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = methodName,
                    TestFrameworkName = testFramework,
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
