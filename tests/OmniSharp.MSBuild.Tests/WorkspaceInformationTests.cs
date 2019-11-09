using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class WorkspaceInformationTests : AbstractMSBuildTestFixture
    {
        public WorkspaceInformationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task TestProjectAndSolution()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.Equal("ProjectAndSolution.sln", Path.GetFileName(workspaceInfo.SolutionPath));
                Assert.NotNull(workspaceInfo.Projects);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("ProjectAndSolution", project.AssemblyName);
                Assert.Equal("bin/Debug/netcoreapp2.1/", project.OutputPath.EnsureForwardSlashes());
                Assert.Equal("obj/Debug/netcoreapp2.1/", project.IntermediateOutputPath.EnsureForwardSlashes());
                var expectedTargetPath = $"{testProject.Directory}/{project.OutputPath}ProjectAndSolution.dll".EnsureForwardSlashes();
                Assert.Equal(expectedTargetPath, project.TargetPath.EnsureForwardSlashes());
                Assert.Equal("Debug", project.Configuration);
                Assert.Equal("AnyCPU", project.Platform);
                Assert.True(project.IsExe);
                Assert.False(project.IsUnityProject);

                Assert.Equal(".NETCoreApp,Version=v2.1", project.TargetFramework);
                var targetFramework = Assert.Single(project.TargetFrameworks);
                Assert.Equal("netcoreapp2.1", targetFramework.ShortName);
            }
        }

        [Fact]
        public async Task ProjectAndSolutionWithProjectSection()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolutionWithProjectSection"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.Equal("ProjectAndSolutionWithProjectSection.sln", Path.GetFileName(workspaceInfo.SolutionPath));
                Assert.NotNull(workspaceInfo.Projects);
                var project = Assert.Single(workspaceInfo.Projects);
                Assert.Equal(".NETCoreApp,Version=v2.1", project.TargetFramework);
                Assert.Equal("netcoreapp2.1", project.TargetFrameworks[0].ShortName);
            }
        }

        [Fact]
        public async Task NetCore30Project()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("NetCore30Project"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(workspaceInfo.Projects);
                var project = Assert.Single(workspaceInfo.Projects);
                Assert.Equal("NetCore30Project", project.AssemblyName);
                Assert.Equal(".NETCoreApp,Version=v3.0", project.TargetFramework);
                Assert.Equal("netcoreapp3.0", project.TargetFrameworks[0].ShortName);
            }
        }

        [Fact]
        public async Task TwoProjectsWithSolution()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolution"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.Equal("TwoProjectsWithSolution.sln", Path.GetFileName(workspaceInfo.SolutionPath));
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(2, workspaceInfo.Projects.Count);

                var firstProject = workspaceInfo.Projects[0];
                Assert.Equal("App.csproj", Path.GetFileName(firstProject.Path));
                Assert.Equal(".NETCoreApp,Version=v2.1", firstProject.TargetFramework);
                Assert.Equal("netcoreapp2.1", firstProject.TargetFrameworks[0].ShortName);

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
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(workspaceInfo.Projects);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("ProjectWithGeneratedFile.csproj", Path.GetFileName(project.Path));
                Assert.Equal(4, project.SourceFiles.Count);
            }
        }

        [Fact]
        public async Task ProjectWithSdkProperty()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithSdkProperty"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(workspaceInfo.Projects);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("ProjectWithSdkProperty.csproj", Path.GetFileName(project.Path));
                Assert.Equal(3, project.SourceFiles.Count);
            }
        }

        [Fact]
        public async Task CSharpAndFSharp()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CSharpAndFSharp"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(workspaceInfo.Projects);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("csharp-console.csproj", Path.GetFileName(project.Path));
                Assert.Equal(3, project.SourceFiles.Count);
            }
        }

        [Fact]
        public async Task TestProjectWithReferencedProjectOutsideOfOmniSharp()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolution"))
            using (var host = CreateMSBuildTestHost(Path.Combine(testProject.Directory, "App")))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(2, workspaceInfo.Projects.Count);

                var project1 = workspaceInfo.Projects[0];
                Assert.Equal("App.csproj", Path.GetFileName(project1.Path));

                var project2 = workspaceInfo.Projects[1];
                Assert.Equal("Lib.csproj", Path.GetFileName(project2.Path));
            }
        }

        [Fact]
        public async Task TestProjectWithSignedReferencedProject()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("SolutionWithSignedProject"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(2, workspaceInfo.Projects.Count);

                // For the test to validate that assemblies representing targets of loaded projects are being skipped,
                // the assemblies must be present on disk.
                foreach (var loadedProject in workspaceInfo.Projects)
                {
                    Assert.True(File.Exists(loadedProject.TargetPath),
                        $"Project target assembly is not found {loadedProject.TargetPath}. " +
                        $"Make sure to build the whole repo using the build script before running the test.");
                }

                // The callee assembly must be signed to ensure that in case of a bug the assembly is loaded
                // "The type 'Callee' exists in both ..." is present as a code check (which is validated below).
                var signedProject = workspaceInfo.Projects.SingleOrDefault(p => p.AssemblyName.Equals("CalleeLib", StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(signedProject);
                var token = BitConverter.ToString(AssemblyName.GetAssemblyName(signedProject.TargetPath).GetPublicKeyToken());
                Assert.Equal("A5-D8-5A-5B-AA-39-A6-A6", token, ignoreCase: true);

                var response = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "CallerLib", "Caller.cs"));
                // Log result to easier debugging of the test should it fail during automated valdiation
                foreach (var fix in response.QuickFixes)
                {
                    host.Logger.LogInformation($"Unexpected QuickFix returned for {fix.FileName}: {fix.Text}");
                }

                Assert.Empty(response.QuickFixes);
            }
        }

        [Fact]
        public async Task TestProjectWithMultiTFMReferencedProjectOutsideOfOmniSharp()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMultiTFMLib"))
            using (var host = CreateMSBuildTestHost(Path.Combine(testProject.Directory, "App")))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

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
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(workspaceInfo.Projects);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal(6, project.SourceFiles.Count);
                Assert.Contains(project.SourceFiles, fileName => fileName.EndsWith("GrammarParser.cs"));
            }    
        }

        [Fact]
        public async Task ProjectWithWildcardPackageReference()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithWildcardPackageReference"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

                Assert.NotNull(workspaceInfo.Projects);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("ProjectWithWildcardPackageReference.csproj", Path.GetFileName(project.Path));
                Assert.Equal(3, project.SourceFiles.Count);
            }
        }

        [Fact]
        public async Task DoesntParticipateInWorkspaceInfoResponseWhenDisabled()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution"))
            using (var host = CreateOmniSharpHost(testProject.Directory, configurationData: new Dictionary<string, string>
            {
                ["msbuild:enabled"] = "false"
            }))
            {
                var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();
                Assert.Null(workspaceInfo);
            }
        }
    }
}
