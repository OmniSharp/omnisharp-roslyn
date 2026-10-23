using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TestUtility;
using Xunit;

namespace OmniSharp.MSBuild.Tests
{
    public class WorkspaceProjectInformationTests : AbstractMSBuildTestFixture
    {
        public WorkspaceProjectInformationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task TwoProjectWithGeneratedFile()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithGeneratedFile");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);

            Assert.Equal("ProjectWithGeneratedFile.csproj", Path.GetFileName(project.Path));
            Assert.Equal(4, project.SourceFiles.Count);
        }

        [Fact]
        public async Task ProjectWithSdkProperty()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithSdkProperty");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);

            Assert.Equal("ProjectWithSdkProperty.csproj", Path.GetFileName(project.Path));
            Assert.Equal(3, project.SourceFiles.Count);
        }

        [Fact]
        public async Task CSharpAndFSharp()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("CSharpAndFSharp");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);

            Assert.Equal("csharp-console.csproj", Path.GetFileName(project.Path));
            Assert.Equal(3, project.SourceFiles.Count);
        }

        [ConditionalFact(typeof(WindowsOnly))]
        public async Task AntlrGeneratedFiles()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("AntlrGeneratedFiles");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);

            Assert.Equal(6, project.SourceFiles.Count);
            Assert.Contains(project.SourceFiles, fileName => fileName.EndsWith("GrammarParser.cs"));
        }

        [Fact]
        public async Task ProjectWithWildcardPackageReference()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithWildcardPackageReference");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);

            Assert.Equal("ProjectWithWildcardPackageReference.csproj", Path.GetFileName(project.Path));
            Assert.Equal(3, project.SourceFiles.Count);
        }

        [Fact]
        public async Task DoesntParticipateInWorkspaceInfoResponseWhenDisabled()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution");
            using var host = CreateOmniSharpHost(testProject.Directory, configurationData: new Dictionary<string, string>
            {
                ["msbuild:enabled"] = "false"
            });
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();
            Assert.Null(workspaceInfo);
        }
    }
}
