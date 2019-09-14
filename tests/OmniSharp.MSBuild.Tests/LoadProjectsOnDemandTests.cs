using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.MembersTree;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Models.V2.CodeStructure;
using OmniSharp.MSBuild.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Services.Structure;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class LoadProjectsOnDemandTests : AbstractMSBuildTestFixture
    {
        public LoadProjectsOnDemandTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task LoadOnDemandProjectsOneByOne()
        {
            var configData = new Dictionary<string, string> { [$"MsBuild:{nameof(MSBuildOptions.LoadProjectsOnDemand)}"] = "true" };
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolution"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: configData))
            {
                MSBuildWorkspaceInfo workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                // Expect empty workspace initially since no documents have been requested yet
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(0, workspaceInfo.Projects.Count);

                // Requesting library document should load only that project
                GetCodeActionsService codeActionHandler = host.GetRequestHandler<GetCodeActionsService>(OmniSharpEndpoints.V2.GetCodeActions);
                GetCodeActionsResponse codeActionResponse = await codeActionHandler.Handle(
                    new GetCodeActionsRequest { FileName = Path.Combine(testProject.Directory, "Lib", "Class1.cs") });
                workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(codeActionResponse);
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(1, workspaceInfo.Projects.Count);
                Assert.Equal("Lib.csproj", Path.GetFileName(workspaceInfo.Projects[0].Path));

                // Requesting app document should load that project as well
                QuickFixResponse codeCheckResponse = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "App", "Program.cs"));
                workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(codeCheckResponse);
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(2, workspaceInfo.Projects.Count);
                Assert.Equal("App.csproj", Path.GetFileName(workspaceInfo.Projects[0].Path));
                Assert.Equal("Lib.csproj", Path.GetFileName(workspaceInfo.Projects[1].Path));
            }
        }

        [Fact]
        public async Task LoadOnDemandProjectAndItsReference()
        {
            var configData = new Dictionary<string, string> { [$"MsBuild:{nameof(MSBuildOptions.LoadProjectsOnDemand)}"] = "true" };
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolution"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: configData))
            {
                MSBuildWorkspaceInfo workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                // Expect empty workspace initially since no documents have been requested yet
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(0, workspaceInfo.Projects.Count);

                // Requesting app document should load both projects
                MembersAsTreeService membersAsTreeService = host.GetRequestHandler<MembersAsTreeService>(OmniSharpEndpoints.MembersTree);
                var request = new MembersTreeRequest { FileName = Path.Combine(testProject.Directory, "App", "Program.cs") };
                FileMemberTree response = await membersAsTreeService.Handle(request);
                workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(request);
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(2, workspaceInfo.Projects.Count);
                Assert.Equal("App.csproj", Path.GetFileName(workspaceInfo.Projects[0].Path));
                Assert.Equal("Lib.csproj", Path.GetFileName(workspaceInfo.Projects[1].Path));
            }
        }

        [Fact]
        public async Task LoadOnDemandProjectWithTwoLevelsOfTransitiveReferences()
        {
            var configData = new Dictionary<string, string> { [$"MsBuild:{nameof(MSBuildOptions.LoadProjectsOnDemand)}"] = "true" };
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("DeepProjectTransitiveReference"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: configData))
            {
                MSBuildWorkspaceInfo workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                // Expect empty workspace initially since no documents have been requested yet
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(0, workspaceInfo.Projects.Count);

                // Requesting the document should load project App, its reference Lib1 and Lib2 that is referenced by Lib1
                MembersAsTreeService membersAsTreeService = host.GetRequestHandler<MembersAsTreeService>(OmniSharpEndpoints.MembersTree);
                var request = new MembersTreeRequest { FileName = Path.Combine(testProject.Directory, "App", "Program.cs") };
                FileMemberTree response = await membersAsTreeService.Handle(request);
                workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(request);
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(3, workspaceInfo.Projects.Count);
                Assert.Equal("App.csproj", Path.GetFileName(workspaceInfo.Projects[0].Path));
                Assert.Equal("Lib1.csproj", Path.GetFileName(workspaceInfo.Projects[1].Path));
                Assert.Equal("Lib2.csproj", Path.GetFileName(workspaceInfo.Projects[2].Path));
            }
        }
    }
}
