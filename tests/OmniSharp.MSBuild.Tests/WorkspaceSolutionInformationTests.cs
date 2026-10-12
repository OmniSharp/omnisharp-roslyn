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
    public class WorkspaceSolutionInformationTests : AbstractMSBuildTestFixture
    {
        public WorkspaceSolutionInformationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task TestProjectAndSolution()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.Equal("ProjectAndSolution.sln", Path.GetFileName(workspaceInfo.SolutionPath));
            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);

            Assert.Equal("ProjectAndSolution", project.AssemblyName);
            Assert.Equal("bin/Debug/net8.0/", project.OutputPath.EnsureForwardSlashes());
            Assert.Equal("obj/Debug/net8.0/", project.IntermediateOutputPath.EnsureForwardSlashes());
            var expectedTargetPath = $"{testProject.Directory}/{project.OutputPath}ProjectAndSolution.dll".EnsureForwardSlashes();
            Assert.Equal(expectedTargetPath, project.TargetPath.EnsureForwardSlashes());
            Assert.Equal("Debug", project.Configuration);
            Assert.Equal("AnyCPU", project.Platform);
            Assert.True(project.IsExe);
            Assert.False(project.IsUnityProject);

            Assert.Equal(".NETCoreApp,Version=v8.0", project.TargetFramework);
            var targetFramework = Assert.Single(project.TargetFrameworks);
            Assert.Equal("net8.0", targetFramework.ShortName);
        }

        [Fact]
        public async Task SlnxSolutionIsDiscoveredAndLoaded()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolutionX");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.Equal("ProjectAndSolutionX.slnx", Path.GetFileName(workspaceInfo.SolutionPath));
            var project = Assert.Single(workspaceInfo.Projects);
            Assert.Equal("ProjectAndSolutionX", project.AssemblyName);
            Assert.Equal("ProjectAndSolutionX.csproj", Path.GetFileName(project.Path));
            Assert.Equal(".NETCoreApp,Version=v8.0", project.TargetFramework);
        }

        [Fact]
        public async Task TestProjectAndSolutionFilter()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolutionFilter"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.Equal("ProjectAndSolutionFilter.slnf", Path.GetFileName(workspaceInfo.SolutionPath));
                Assert.NotNull(workspaceInfo.Projects);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("ProjectAndSolutionFilter", project.AssemblyName);
                Assert.Equal("bin/Debug/net8.0/", project.OutputPath.EnsureForwardSlashes());
                Assert.Equal("obj/Debug/net8.0/", project.IntermediateOutputPath.EnsureForwardSlashes());
                var expectedTargetPath = $"{testProject.Directory}/Project/{project.OutputPath}ProjectAndSolutionFilter.dll".EnsureForwardSlashes();
                Assert.Equal(expectedTargetPath, project.TargetPath.EnsureForwardSlashes());
                Assert.Equal("Debug", project.Configuration);
                Assert.Equal("AnyCPU", project.Platform);
                Assert.True(project.IsExe);
                Assert.False(project.IsUnityProject);

                Assert.Equal(".NETCoreApp,Version=v8.0", project.TargetFramework);
                var targetFramework = Assert.Single(project.TargetFrameworks);
                Assert.Equal("net8.0", targetFramework.ShortName);
            }
        }

        [Fact]
        public async Task ProjectAndSolutionWithProjectSection()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolutionWithProjectSection");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.Equal("ProjectAndSolutionWithProjectSection.sln", Path.GetFileName(workspaceInfo.SolutionPath));
            Assert.NotNull(workspaceInfo.Projects);
            var project = Assert.Single(workspaceInfo.Projects);
            Assert.Equal(".NETCoreApp,Version=v8.0", project.TargetFramework);
            Assert.Equal("net8.0", project.TargetFrameworks[0].ShortName);
        }

        [Fact]
        public async Task TwoProjectsWithSolution()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolution");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.Equal("TwoProjectsWithSolution.sln", Path.GetFileName(workspaceInfo.SolutionPath));
            Assert.NotNull(workspaceInfo.Projects);
            Assert.Equal(2, workspaceInfo.Projects.Count);

            var firstProject = workspaceInfo.Projects[0];
            Assert.Equal("App.csproj", Path.GetFileName(firstProject.Path));
            Assert.Equal(new Guid("632DFE45-B56E-4158-8F27-45E2BA0BAFCF"), firstProject.ProjectGuid);
            Assert.Equal(".NETCoreApp,Version=v8.0", firstProject.TargetFramework);
            Assert.Equal("net8.0", firstProject.TargetFrameworks[0].ShortName);

            var secondProject = workspaceInfo.Projects[1];
            Assert.Equal("Lib.csproj", Path.GetFileName(secondProject.Path));
            Assert.Equal(new Guid("CE41561B-5D13-4688-8686-EEFF744BE8B5"), secondProject.ProjectGuid);
            Assert.Equal(".NETStandard,Version=v2.0", secondProject.TargetFramework);
            Assert.Equal("netstandard2.0", secondProject.TargetFrameworks[0].ShortName);
        }

        [Fact]
        public async Task TwoProjectsWithSolutionAndCustomConfigurations()
        {
            var configData = new Dictionary<string, string> { [$"MsBuild:{nameof(Options.MSBuildOptions.Configuration)}"] = "ReleaseSln" };
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolutionAndCustomConfigurations");
            using var host = CreateMSBuildTestHost(testProject.Directory, configurationData: configData.ToConfiguration());
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.Equal("TwoProjectsWithSolutionAndCustomConfigurations.sln", Path.GetFileName(workspaceInfo.SolutionPath));
            Assert.NotNull(workspaceInfo.Projects);
            Assert.Equal(2, workspaceInfo.Projects.Count);

            var firstProject = workspaceInfo.Projects[0];
            Assert.Equal("App.csproj", Path.GetFileName(firstProject.Path));
            Assert.Equal("Release1", firstProject.Configuration);
            Assert.Equal(".NETCoreApp,Version=v8.0", firstProject.TargetFramework);
            Assert.Equal("net8.0", firstProject.TargetFrameworks[0].ShortName);

            var secondProject = workspaceInfo.Projects[1];
            Assert.Equal("Lib.csproj", Path.GetFileName(secondProject.Path));
            Assert.Equal("Release2", secondProject.Configuration);
            Assert.Equal(".NETStandard,Version=v2.0", secondProject.TargetFramework);
            Assert.Equal("netstandard2.0", secondProject.TargetFrameworks[0].ShortName);
        }
    }
}
