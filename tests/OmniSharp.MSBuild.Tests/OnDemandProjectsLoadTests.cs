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
    public class OnDemandProjectsLoadTests : AbstractMSBuildTestFixture
    {
        public OnDemandProjectsLoadTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task LoadProjectsOnDemandOneByOne()
        {
            var configData = new Dictionary<string, string> { [$"MsBuild:{nameof(MSBuildOptions.OnDemandProjectsLoad)}"] = "true" };
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
        public async Task LoadProjectAndItsReferenceOnDemand()
        {
            var configData = new Dictionary<string, string> { [$"MsBuild:{nameof(MSBuildOptions.OnDemandProjectsLoad)}"] = "true" };
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
        public async Task OnDemandProjectsLoadSearchStopsAtFile()
        {
            var configData = new Dictionary<string, string> { [$"MsBuild:{nameof(MSBuildOptions.OnDemandProjectsLoad)}"] = "true" };
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolution"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: configData))
            {
                MSBuildWorkspaceInfo workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                // Expect empty workspace initially since no documents have been requested yet
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(0, workspaceInfo.Projects.Count);

                // Create a subfolder containing both "stop-search-at" marker file ('.git') and the file to load
                string stopAtFolder = Path.Combine(testProject.Directory, "App", "StopHere");
                Directory.CreateDirectory(stopAtFolder);
                string fileToRequest = Path.Combine(stopAtFolder, "Empty.cs");
                File.WriteAllText(fileToRequest, "class empty {}");
                File.WriteAllText(Path.Combine(stopAtFolder, ".git"), string.Empty);

                // Requesting a file should should stop search since it will find stop-search at file in the same folder
                CodeStructureService service = host.GetRequestHandler<CodeStructureService>(OmniSharpEndpoints.V2.CodeStructure);
                var request = new CodeStructureRequest { FileName = fileToRequest };
                CodeStructureResponse response = await service.Handle(request);
                workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                // No project is expected to be found/loaded
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(0, workspaceInfo.Projects.Count);
            }
        }

        [Fact]
        public async Task OnDemandProjectsLoadSearchStopsAtFolder()
        {
            var configData = new Dictionary<string, string> { [$"MsBuild:{nameof(MSBuildOptions.OnDemandProjectsLoad)}"] = "true" };
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolution"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: configData))
            {
                MSBuildWorkspaceInfo workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                // Expect empty workspace initially since no documents have been requested yet
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(0, workspaceInfo.Projects.Count);

                // Create a subfolder containing "stop-search-at" marker folder '.git'
                Directory.CreateDirectory(Path.Combine(testProject.Directory, "App", "StopHere", ".git"));

                string fileToRequestFolder = Path.Combine(testProject.Directory, "App", "StopHere", "EmptyClassLib");
                Directory.CreateDirectory(fileToRequestFolder);
                string fileToRequest = Path.Combine(fileToRequestFolder, "Empty.cs");
                File.WriteAllText(fileToRequest, "class empty {}");

                // Requesting a file should should stop search since it will find stop-search at file in the same folder
                CodeStructureService service = host.GetRequestHandler<CodeStructureService>(OmniSharpEndpoints.V2.CodeStructure);
                var request = new CodeStructureRequest { FileName = fileToRequest };
                CodeStructureResponse response = await service.Handle(request);
                workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                // No project is expected to be found/loaded
                Assert.Null(workspaceInfo.SolutionPath);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(0, workspaceInfo.Projects.Count);
            }
        }
    }
}
