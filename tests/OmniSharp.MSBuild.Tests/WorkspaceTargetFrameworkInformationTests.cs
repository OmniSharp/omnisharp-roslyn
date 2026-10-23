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
    public class WorkspaceTargetFrameworkInformationTests : AbstractMSBuildTestFixture
    {
        public WorkspaceTargetFrameworkInformationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [ConditionalFact(typeof(NonMonoRuntimeOnly))]
        public async Task Net80Project()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net80Project");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);
            Assert.Equal("Net80Project", project.AssemblyName);
            Assert.Equal(".NETCoreApp,Version=v8.0", project.TargetFramework);
            Assert.Contains(project.TargetFrameworks[0].ShortName, new[] { "net80", "net8.0" });
        }

        [ConditionalFact(typeof(NonMonoRuntimeOnly))]
        public async Task Net90Project()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net90Project");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);
            Assert.Equal("Net90Project", project.AssemblyName);
            Assert.Equal(".NETCoreApp,Version=v9.0", project.TargetFramework);
            Assert.Contains(project.TargetFrameworks[0].ShortName, new[] { "net90", "net9.0" });
        }

        [ConditionalFact(typeof(NonMonoRuntimeOnly))]
        public async Task Net100Project()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net100Project");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);
            Assert.Equal("Net100Project", project.AssemblyName);
            Assert.Equal(".NETCoreApp,Version=v10.0", project.TargetFramework);
            Assert.Contains(project.TargetFrameworks[0].ShortName, new[] { "net100", "net10.0" });
        }

        [ConditionalFact(typeof(NonMonoRuntimeOnly))]
        public async Task Net110Project()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net110Project");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);
            Assert.Equal("Net110Project", project.AssemblyName);
            Assert.Equal(".NETCoreApp,Version=v11.0", project.TargetFramework);
            Assert.Contains(project.TargetFrameworks[0].ShortName, new[] { "net110", "net11.0" });
        }

        [Fact]
        public async Task TestProjectWithMultiTFMReferencedProjectOutsideOfOmniSharp()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMultiTFMLib");
            using var host = CreateMSBuildTestHost(Path.Combine(testProject.Directory, "App"));
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            Assert.Equal(2, workspaceInfo.Projects.Count);

            var project1 = workspaceInfo.Projects[0];
            Assert.Equal("App.csproj", Path.GetFileName(project1.Path));

            var project2 = workspaceInfo.Projects[1];
            Assert.Equal("Lib.csproj", Path.GetFileName(project2.Path));
            Assert.Equal(".NETStandard,Version=v1.5", project2.TargetFramework);
            Assert.Equal(2, project2.TargetFrameworks.Count);
        }
    }
}
