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
    public abstract class RunTestsInContextTestBase : AbstractTestFixture
    {
        protected RunTestsInContextTestBase(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        public override DotNetCliVersion DotNetCliVersion { get; } = DotNetCliVersion.Current;

        protected async Task<(string[]? MethodNames, string? TestFramework)> GetContextTestMethodNamesAsync(string projectName, int line, int column, string fileName = "TestProgram.cs")
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName);
            using var host = CreateOmniSharpHost(testProject.Directory, null, DotNetCliVersion);
            var document = host.Workspace.GetDocument(Path.Combine(testProject.Directory, fileName));

            Assert.NotNull(document);

            using var testManager = TestManager.Create(
                document.Project,
                host.GetExport<IDotNetCliService>(),
                host.GetExport<IEventEmitter>(),
                host.LoggerFactory);

            return await testManager.GetContextTestMethodNames(line, column, document, CancellationToken.None);
        }

        protected static string GetTestFramework(string projectName) =>
            projectName switch
            {
                XunitTestProject => "xunit",
                NUnitTestProject => "nunit",
                MSTestProject => "mstest",
                _ => throw new Xunit.Sdk.XunitException($"Unknown test project '{projectName}'.")
            };

        protected async Task<RunTestResponse> RunDotNetTestAsync(string projectName, int line, int column, bool shouldPass, string? targetFrameworkVersion = null, bool expectResults = true, bool useRunSettings = false, string fileName = "TestProgram.cs")
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, null, DotNetCliVersion))
            {
                var service = host.GetRequestHandler<RunTestsInContextService>(OmniSharpEndpoints.V2.RunTestsInContext);

                var request = new RunTestsInContextRequest
                {
                    FileName = Path.Combine(testProject.Directory, fileName),
                    Line = line,
                    Column = column,
                    TargetFrameworkVersion = targetFrameworkVersion,
                    NoBuild = true
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
                    Assert.Null(response.Failure);
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
