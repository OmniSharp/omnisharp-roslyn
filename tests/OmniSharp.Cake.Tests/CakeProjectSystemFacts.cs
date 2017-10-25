using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild.Models;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class CakeProjectSystemFacts : AbstractTestFixture
    {
        public CakeProjectSystemFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task ShouldGetProjects()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);

                Assert.Equal(2, workspaceInfo.Projects.Count());
                Assert.Contains("build.cake", workspaceInfo.Projects.Select(p => Path.GetFileName(p.Path)));
                Assert.Contains("foo.cake", workspaceInfo.Projects.Select(p => Path.GetFileName(p.Path)));
            }
        }

        private static async Task<CakeContextModelCollection> GetWorkspaceInfoAsync(OmniSharpTestHost host)
        {
            var service = host.GetWorkspaceInformationService();

            var request = new WorkspaceInformationRequest
            {
                ExcludeSourceFiles = false
            };

            var response = await service.Handle(request);

            return (CakeContextModelCollection)response["Cake"];
        }
    }
}
