using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public abstract class AbstractGetTestStartInfoFacts : AbstractSingleRequestHandlerTestFixture<GetTestStartInfoService>
    {
        protected const string LegacyXunitTestProject = "LegacyXunitTestProject";
        protected const string LegacyNunitTestProject = "LegacyNunitTestProject";
        protected const string XunitTestProject = "XunitTestProject";
        protected const string NunitTestProject = "NunitTestProject";

        protected AbstractGetTestStartInfoFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName { get; } = OmnisharpEndpoints.V2.GetTestStartInfo;

        public abstract bool UseLegacyDotNetCli { get; }

        protected async Task GetDotNetTestStartInfoAsync(string projectName, string methodName, string testFramework)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, useLegacyDotNetCli: UseLegacyDotNetCli))
            {
                var service = GetRequestHandler(host);

                var request = new GetTestStartInfoRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = methodName,
                    TestFrameworkName = testFramework
                };

                var response = await service.Handle(request);

                var dotNetCli = host.GetExport<DotNetCliService>();

                Assert.Equal(dotNetCli.DotNetPath, response.Executable);
            }
        }

    }
}
