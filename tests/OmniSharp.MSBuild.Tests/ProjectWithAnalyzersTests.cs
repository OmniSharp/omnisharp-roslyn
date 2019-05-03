using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectWithAnalyzersTests : AbstractMSBuildTestFixture
    {
        public ProjectWithAnalyzersTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task WhenProjectHasAnalyzersItDoesntLockAnalyzerDlls()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers"))
            {
                using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true)))
                {
                    await host.RestoreProject(testProject);

                    // TODO: Remove this before merge ...
                    Thread.Sleep(5 * 1000);

                    var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));
                    var analyzerReferences = host.Workspace.CurrentSolution.Projects.Single().AnalyzerReferences.ToList();

                    Assert.NotEmpty(analyzerReferences);
                    Assert.NotEmpty(diagnostics.QuickFixes);

                    // This should not throw when analyzers are shadow copied.
                    Directory.Delete(Path.Combine(testProject.Directory, "./nugets"), true);
                }
            }
        }

        [Fact]
        public async Task LoadProjectWithNewCompilationOptions()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var project = host.Workspace.CurrentSolution.Projects.Single();

                Assert.Contains(project.CompilationOptions.SpecificDiagnosticOptions, x => x.Key == "CA1021");
            }
        }

        private static async Task RestoreProject(ITestProject testProject)
        {
            await new DotNetCliService(new LoggerFactory(), NullEventEmitter.Instance).RestoreAsync(testProject.Directory);
        }
    }
}
