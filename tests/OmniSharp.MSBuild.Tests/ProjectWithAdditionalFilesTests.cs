using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectWithAdditionalFilesTests : AbstractMSBuildTestFixture
    {
        public ProjectWithAdditionalFilesTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task WhenProjectIsNoAdditionalFiles()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAdditionalFiles"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var project = host.Workspace.CurrentSolution.Projects.Where(x => x.AssemblyName == "ProjectWithNoAdditionalFiles").Single();
                Assert.Empty(project.AdditionalDocuments);
            }
        }

        [Fact]
        public async Task WhenProjectIsSingleAdditionalFiles()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAdditionalFiles"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var project = host.Workspace.CurrentSolution.Projects.Where(x => x.AssemblyName == "ProjectWithSingleAdditionalFiles").Single();
                Assert.Single(project.AdditionalDocuments);

                var doc = project.AdditionalDocuments.Single();
                Assert.Equal("dummy0.txt", doc.Name);
                var text = await doc.GetTextAsync();
                Assert.Equal("Dummy0", text.ToString());
            }
        }

        [Fact]
        public async Task WhenProjectIsMultiAdditionalFiles()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAdditionalFiles"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var project = host.Workspace.CurrentSolution.Projects.Where(x => x.AssemblyName == "ProjectWithMultiAdditionalFiles").Single();
                var additionalDocuments = project.AdditionalDocuments.ToList();
                Assert.Equal(3, additionalDocuments.Count());

                for (int i = 0; i < 3; i++)
                {
                    var doc = additionalDocuments[i];
                    Assert.Equal(string.Format("dummy{0}.txt", i), doc.Name);
                    var text = await additionalDocuments[i].GetTextAsync();
                    Assert.Equal(string.Format("Dummy{0}", i), text.ToString());
                }
            }
        }
    }
}
