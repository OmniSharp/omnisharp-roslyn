using System;
using System.IO;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using OmniSharp.Services;
using OmniSharp.Utilities;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public abstract class AbstractGetTestStartInfoFacts : AbstractTestFixture
    {
        protected AbstractGetTestStartInfoFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        internal GetTestStartInfoService GetRequestHandler(OmniSharpTestHost host)
        {
            return host.GetRequestHandler<GetTestStartInfoService>(OmniSharpEndpoints.V2.GetTestStartInfo);
        }

        protected async Task GetDotNetTestStartInfoAsync(string projectName, string methodName, string testFramework, string targetFrameworkVersion = null, Action<GetTestStartInfoResponse, OmniSharpTestHost> assert = null)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, null, DotNetCliVersion))
            {
                var service = GetRequestHandler(host);

                var request = new GetTestStartInfoRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = methodName,
                    TestFrameworkName = testFramework,
                    TargetFrameworkVersion = targetFrameworkVersion
                };

                var response = await service.Handle(request);

                if (assert != null)
                {
                    assert(response, host);
                }
                else
                {
                    var dotNetCli = host.GetExport<IDotNetCliService>();
                    Assert.Equal(dotNetCli.DotNetPath, response.Executable);
                }
            }
        }
    }
}
