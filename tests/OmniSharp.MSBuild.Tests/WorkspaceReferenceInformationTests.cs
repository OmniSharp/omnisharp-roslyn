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
    public class WorkspaceReferenceInformationTests : AbstractMSBuildTestFixture
    {
        public WorkspaceReferenceInformationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task TestProjectWithReferencedProjectOutsideOfOmniSharp()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolution");
            using var host = CreateMSBuildTestHost(Path.Combine(testProject.Directory, "App"));
            var workspaceInfo = await host.RequestMSBuildWorkspaceInfoAsync();

            Assert.NotNull(workspaceInfo.Projects);
            Assert.Equal(2, workspaceInfo.Projects.Count);

            var project1 = workspaceInfo.Projects[0];
            Assert.Equal("App.csproj", Path.GetFileName(project1.Path));

            var project2 = workspaceInfo.Projects[1];
            Assert.Equal("Lib.csproj", Path.GetFileName(project2.Path));
        }

        [Fact]
        public async Task TestProjectWithSignedReferencedProject()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("SolutionWithSignedProject");
            using var host = CreateMSBuildTestHost(testProject.Directory);
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

        [Fact]
        public async Task TestProjectWithAliasOnProjectReference()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ExternAlias");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var project = host
                .Workspace
                .CurrentSolution
                .Projects
                .Single(x => x.AssemblyName == "ExternAlias.App2");

            var lib = host
                .Workspace
                .CurrentSolution
                .Projects
                .Single(x => x.AssemblyName == "ExternAlias.Lib");

            var projectReference = project
                .ProjectReferences
                .Single(x => x.ProjectId == lib.Id);

            Assert.Collection(projectReference.Aliases,
                referenceA => Assert.Equal("abc", referenceA),
                referenceB => Assert.Equal("def", referenceB));
        }

        [Fact]
        public async Task TestProjectWithAliasOnReference()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ExternAlias");
            using var host = CreateMSBuildTestHost(testProject.Directory);
            var project = host
                .Workspace
                .CurrentSolution
                .Projects
                .Single(x => x.AssemblyName == "ExternAlias.App");

            var reference = project
                .MetadataReferences
                .Single(x => x.Display == "ExternAlias.Lib.dll");

            Assert.Collection(reference.Properties.Aliases,
                referenceA => Assert.Equal("abc", referenceA),
                referenceB => Assert.Equal("def", referenceB));
        }
    }
}
