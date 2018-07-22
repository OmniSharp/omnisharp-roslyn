using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.DotNet.Models;
using OmniSharp.Models.WorkspaceInformation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNet.Tests
{
    public class WorkspaceInformationTests : AbstractTestFixture
    {
        private static readonly Dictionary<string, string> s_configurationData = new Dictionary<string, string>
        {
            ["DotNet:Enabled"] = "true"
        };

        public WorkspaceInformationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [ConditionalFact(typeof(IsLegacyTest))]
        public async Task TestSimpleProject()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("LegacyProjectJsonApp", legacyProject: true))
            using (var host = CreateOmniSharpHost(testProject.Directory, s_configurationData))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Single(workspaceInfo.Projects);

                var project = workspaceInfo.Projects[0];
                Assert.Equal("LegacyProjectJsonApp", project.Name);
                Assert.Equal("netcoreapp1.1", project.Frameworks[0].ShortName);

                Assert.Equal(2, project.Configurations.Count);
                Assert.Contains(project.Configurations, c => c.Name == "Debug");
                Assert.Contains(project.Configurations, c => c.Name == "Release");
                Assert.True(project.Configurations.All(c => c.EmitEntryPoint == true));

                Assert.Single(project.SourceFiles);
            }
        }

        [ConditionalFact(typeof(IsLegacyTest))]
        public async Task TestMSTestProject()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("LegacyMSTestProject", legacyProject: true))
            using (var host = CreateOmniSharpHost(testProject.Directory, s_configurationData))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Single(workspaceInfo.Projects);

                var project = workspaceInfo.Projects[0];
                Assert.Equal("LegacyMSTestProject", project.Name);
                Assert.Equal("netcoreapp1.0", project.Frameworks[0].ShortName);

                Assert.Equal(2, project.Configurations.Count);
                Assert.Contains(project.Configurations, c => c.Name == "Debug");
                Assert.Contains(project.Configurations, c => c.Name == "Release");

                Assert.Single(project.SourceFiles);
            }
        }

        [ConditionalFact(typeof(IsLegacyTest))]
        public async Task TestNUnitProject()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("LegacyNUnitTestProject", legacyProject: true))
            using (var host = CreateOmniSharpHost(testProject.Directory, s_configurationData))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Single(workspaceInfo.Projects);

                var project = workspaceInfo.Projects[0];
                Assert.Equal("LegacyNUnitTestProject", project.Name);
                Assert.Equal("netcoreapp1.0", project.Frameworks[0].ShortName);

                Assert.Equal(2, project.Configurations.Count);
                Assert.Contains(project.Configurations, c => c.Name == "Debug");
                Assert.Contains(project.Configurations, c => c.Name == "Release");

                Assert.Single(project.SourceFiles);
            }
        }

        [ConditionalFact(typeof(IsLegacyTest))]
        public async Task TestXunitProject()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("LegacyXunitTestProject", legacyProject: true))
            using (var host = CreateOmniSharpHost(testProject.Directory, s_configurationData))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.NotNull(workspaceInfo.Projects);
                Assert.Single(workspaceInfo.Projects);

                var project = workspaceInfo.Projects[0];
                Assert.Equal("LegacyXunitTestProject", project.Name);
                Assert.Equal("netcoreapp1.0", project.Frameworks[0].ShortName);

                Assert.Equal(2, project.Configurations.Count);
                Assert.Contains(project.Configurations, c => c.Name == "Debug");
                Assert.Contains(project.Configurations, c => c.Name == "Release");

                Assert.Single(project.SourceFiles);
            }
        }

        private static async Task<DotNetWorkspaceInfo> GetWorkspaceInfoAsync(OmniSharpTestHost host)
        {
            var service = host.GetWorkspaceInformationService();

            var request = new WorkspaceInformationRequest
            {
                ExcludeSourceFiles = false
            };

            var response = await service.Handle(request);

            return (DotNetWorkspaceInfo)response["DotNet"];
        }
    }
}
