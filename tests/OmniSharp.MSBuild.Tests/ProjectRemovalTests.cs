using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.FileWatching;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.FilesChanged;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using static OmniSharp.MSBuild.Tests.ProjectLoadListenerTests;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectRemovalTests : AbstractMSBuildTestFixture
    {
        public ProjectRemovalTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task WhenProjectIsRemoved_RemoveItFromWorkspace()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory)))
            {
                var csprojFile = Path.Combine(testProject.Directory, "ProjectWithAnalyzers.csproj");
                await NotifyFileremoved(host, csprojFile);

                emitter.WaitForProjectUpdate();

                Assert.Empty(host.Workspace.CurrentSolution.Projects);
            }
        }

        private static async Task NotifyFileremoved(OmniSharpTestHost host, string file)
        {
            await host.GetFilesChangedService().Handle(new[] {
                    new FilesChangedRequest() {
                    FileName = file,
                    ChangeType = FileChangeType.Delete
                    }
                });
        }

    }
}
