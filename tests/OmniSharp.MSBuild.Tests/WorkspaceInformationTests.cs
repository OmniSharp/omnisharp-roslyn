﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild.Models;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
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
        public async Task TestProjectWithSignedReferencedProject()
        {
            using (ITestProject testProject = await TestAssets.Instance.GetTestProjectAsync("SolutionWithSignedProject"))
            using (OmniSharpTestHost host = CreateOmniSharpHost(Path.Combine(testProject.Directory)))
            {
                MSBuildWorkspaceInfo workspaceInfo = await GetWorkspaceInfoAsync(host);
                Assert.NotNull(workspaceInfo.Projects);
                Assert.Equal(2, workspaceInfo.Projects.Count);

                // For the test to validate that assemblies representing targets of loaded projects are being skipped,
                // the assemblies must be present on disk.
                foreach (MSBuildProjectInfo loadedProject in workspaceInfo.Projects)
                {
                    Assert.True(File.Exists(loadedProject.TargetPath),
                        $"Project target assembly is not found {loadedProject.TargetPath}. " +
                        $"Make sure to build the whole repo using the build script before running the test.");
                }

                // The callee assembly must be signed to ensure that in case of a bug the assembly is loaded
                // "The type 'Callee' exists in both ..." is present as a code check (which is validated below).
                MSBuildProjectInfo signedProject = workspaceInfo.Projects.SingleOrDefault(p => p.AssemblyName.Equals("CalleeLib", StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(signedProject);
                string token = BitConverter.ToString(AssemblyName.GetAssemblyName(signedProject.TargetPath).GetPublicKeyToken());
                Assert.Equal("A5-D8-5A-5B-AA-39-A6-A6", token, ignoreCase: true);

                QuickFixResponse quickFixResponse = await GetCodeChecksync(host, Path.Combine(testProject.Directory, "CallerLib\\Caller.cs"));
                // Log result to easier debugging of the test should it fail during automated valdiation
                foreach (QuickFix fix in quickFixResponse.QuickFixes)
                {
                    host.Logger.LogInformation($"Unexpected QuickFix returned for {fix.FileName}: {fix.Text}");
                }

                Assert.Empty(quickFixResponse.QuickFixes);
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

        private static async Task<QuickFixResponse> GetCodeChecksync(OmniSharpTestHost host, string filePath)
        {
            CodeCheckService service = host.GetCodeCheckServiceService();

            var request = new CodeCheckRequest { FileName = filePath };

            return await service.Handle(request);
        }
    }
}
