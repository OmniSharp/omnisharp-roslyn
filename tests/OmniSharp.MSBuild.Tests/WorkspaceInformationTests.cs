using System.IO;
using System.Threading.Tasks;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild.Models;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class WorkspaceInformationTests : AbstractTestFixture
    {
        public WorkspaceInformationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task TestProjectAndSolution()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.Equal("ProjectAndSolution.sln", Path.GetFileName(workspaceInfo.SolutionPath));
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(1, workspaceInfo.Projects.Count);
                Assert.Equal(".NETCoreApp,Version=v1.1", workspaceInfo.Projects[0].TargetFramework);
                Assert.Equal("netcoreapp1.1", workspaceInfo.Projects[0].TargetFrameworks[0].ShortName);
            }
        }

        [Fact]
        public async Task ProjectAndSolutionWithProjectSection()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolutionWithProjectSection"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.Equal("ProjectAndSolutionWithProjectSection.sln", Path.GetFileName(workspaceInfo.SolutionPath));
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(1, workspaceInfo.Projects.Count);
                Assert.Equal(".NETCoreApp,Version=v1.1", workspaceInfo.Projects[0].TargetFramework);
                Assert.Equal("netcoreapp1.1", workspaceInfo.Projects[0].TargetFrameworks[0].ShortName);
            }
        }

        private static async Task<MSBuildWorkspaceInfo> GetWorkspaceInfoAsync(OmniSharpTestHost host)
        {
            var service = host.GetWorkspaceInformationService();

            var request = new WorkspaceInformationRequest
            {
                ExcludeSourceFiles = false
            };

            var response = await service.Handle(request);

            return (MSBuildWorkspaceInfo)response["MsBuild"];
        }
    }
}
