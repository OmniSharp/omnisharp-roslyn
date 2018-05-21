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

        [Fact]
        public async Task TwoProjectsWithSolution()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolution"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.Equal("TwoProjectsWithSolution.sln", Path.GetFileName(workspaceInfo.SolutionPath));
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(2, workspaceInfo.Projects.Count);

                var firstProject = workspaceInfo.Projects[0];
                Assert.Equal("App.csproj", Path.GetFileName(firstProject.Path));
                Assert.Equal(".NETCoreApp,Version=v1.1", firstProject.TargetFramework);
                Assert.Equal("netcoreapp1.1", firstProject.TargetFrameworks[0].ShortName);

                var secondProject = workspaceInfo.Projects[1];
                Assert.Equal("Lib.csproj", Path.GetFileName(secondProject.Path));
                Assert.Equal(".NETStandard,Version=v1.3", secondProject.TargetFramework);
                Assert.Equal("netstandard1.3", secondProject.TargetFrameworks[0].ShortName);
            }
        }

        [Fact]
        public async Task TwoProjectWithGeneratedFile()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithGeneratedFile"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(1, workspaceInfo.Projects.Count);

                var project = workspaceInfo.Projects[0];
                Assert.Equal("ProjectWithGeneratedFile.csproj", Path.GetFileName(project.Path));
                Assert.Equal(4, project.SourceFiles.Count);
            }
        }

        [Fact]
        public async Task ProjectWithSdkProperty()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithSdkProperty"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(1, workspaceInfo.Projects.Count);

                var project = workspaceInfo.Projects[0];
                Assert.Equal("ProjectWithSdkProperty.csproj", Path.GetFileName(project.Path));
                Assert.Equal(3, project.SourceFiles.Count);
            }
        }

        [Fact]
        public async Task CSharpAndFSharp()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CSharpAndFSharp"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(1, workspaceInfo.Projects.Count);

                var project = workspaceInfo.Projects[0];
                Assert.Equal("csharp-console.csproj", Path.GetFileName(project.Path));
                Assert.Equal(3, project.SourceFiles.Count);
            }
        }

        [Fact]
        public async Task TestProjectWithReferencedProjectOutsideOfOmniSharp()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolution"))
            using (var host = CreateOmniSharpHost(Path.Combine(testProject.Directory, "App")))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(2, workspaceInfo.Projects.Count);

                var project1 = workspaceInfo.Projects[0];
                Assert.Equal("App.csproj", Path.GetFileName(project1.Path));

                var project2 = workspaceInfo.Projects[1];
                Assert.Equal("Lib.csproj", Path.GetFileName(project2.Path));
            }
        }

        [Fact]
        public async Task TestProjectWithMultiTFMReferencedProjectOutsideOfOmniSharp()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMultiTFMLib"))
            using (var host = CreateOmniSharpHost(Path.Combine(testProject.Directory, "App")))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(2, workspaceInfo.Projects.Count);

                var project1 = workspaceInfo.Projects[0];
                Assert.Equal("App.csproj", Path.GetFileName(project1.Path));

                var project2 = workspaceInfo.Projects[1];
                Assert.Equal("Lib.csproj", Path.GetFileName(project2.Path));
                Assert.Equal(".NETStandard,Version=v1.3", project2.TargetFramework);
                Assert.Equal(2, project2.TargetFrameworks.Count);
            }
        }

        [ConditionalFact(typeof(WindowsOnly))]
        public async Task AntlrGeneratedFiles()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("AntlrGeneratedFiles"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(1, workspaceInfo.Projects.Count);

                var project = workspaceInfo.Projects[0];
                Assert.Equal(6, project.SourceFiles.Count);
                Assert.Contains(project.SourceFiles, fileName => fileName.EndsWith("GrammarParser.cs"));
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
