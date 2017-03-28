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
    /// <summary>
    /// Tests for legacy 'dotnet test' support for project.json support.
    /// </summary>
    public class LegacyRunTestFacts : AbstractSingleRequestHandlerTestFixture<RunDotNetTestService>
    {
        public LegacyRunTestFacts(ITestOutputHelper output)
            : base(output)
        {

        }

        protected override string EndpointName => OmnisharpEndpoints.RunDotNetTest;

        [Fact]
        public async Task RunXunitTest()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("LegacyXunitTestProject"))
            using (var host = CreateOmniSharpHost(testProject.Directory, useLegacyDotNetCli: true))
            {
                var dotNetCli = host.GetExport<DotNetCliService>();
                await dotNetCli.RestoreAsync(testProject.Directory, "--infer-runtimes");

                var service = GetRequestHandler(host);

                var request = new RunDotNetTestRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = "Main.Test.MainTest.Test",
                    TestFrameworkName = "xunit"
                };

                var response = await service.Handle(request);

                Assert.True(response.Pass);
            }
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData1()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("LegacyXunitTestProject"))
            using (var host = CreateOmniSharpHost(testProject.Directory, useLegacyDotNetCli: true))
            {
                var dotNetCli = host.GetExport<DotNetCliService>();
                await dotNetCli.RestoreAsync(testProject.Directory, "--infer-runtimes");

                var service = GetRequestHandler(host);

                var request = new RunDotNetTestRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = "Main.Test.MainTest.DataDrivenTest1",
                    TestFrameworkName = "xunit"
                };

                var response = await service.Handle(request);

                Assert.False(response.Pass);
            }
        }

        [Fact]
        public async Task RunXunitTheoryWithInlineData2()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("LegacyXunitTestProject"))
            using (var host = CreateOmniSharpHost(testProject.Directory, useLegacyDotNetCli: true))
            {
                var dotNetCli = host.GetExport<DotNetCliService>();
                await dotNetCli.RestoreAsync(testProject.Directory, "--infer-runtimes");

                var service = GetRequestHandler(host);

                var request = new RunDotNetTestRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    MethodName = "Main.Test.MainTest.DataDrivenTest2",
                    TestFrameworkName = "xunit"
                };

                var response = await service.Handle(request);

                Assert.True(response.Pass);
            }
        }
    }
}
