using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.MSBuild.SolutionParsing;
using TestUtility;
using Xunit;

namespace OmniSharp.MSBuild.Tests
{
    public class SolutionParsingTests
    {
        [Theory]
        [InlineData("example.sln")]
        [InlineData("example.SLNX")]
        public void IsSolutionFileFilename_accepts_supported_formats(string filename)
        {
            Assert.True(SolutionFileReader.IsSolutionFileFilename(filename));
        }

        [Fact]
        public void TryRead_returns_false_for_an_unsupported_format()
        {
            Assert.False(SolutionFileReader.TryRead("example.txt", out var projects));
            Assert.Empty(projects);
        }

        [Fact]
        public async Task TryRead_reads_projects_from_an_slnx_file()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolutionX");
            var solutionPath = Path.Combine(testProject.Directory, "ProjectAndSolutionX.slnx");

            Assert.True(SolutionFileReader.TryRead(solutionPath, out var projects));

            var project = Assert.Single(projects);
            Assert.Equal(
                Path.Combine(testProject.Directory, "ProjectAndSolutionX.csproj"),
                project.ProjectPath);
            Assert.NotEqual(default, new System.Guid(project.ProjectGuid));
        }

        [Fact]
        public async Task TryRead_preserves_project_configuration_mappings()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithSolutionAndCustomConfigurations");
            var solutionPath = Path.Combine(testProject.Directory, "TwoProjectsWithSolutionAndCustomConfigurations.sln");

            Assert.True(SolutionFileReader.TryRead(solutionPath, out var projects));

            var app = projects.Single(project => Path.GetFileName(project.ProjectPath) == "App.csproj");
            var library = projects.Single(project => Path.GetFileName(project.ProjectPath) == "Lib.csproj");
            Assert.Equal("Release1|Any CPU", app.SolutionConfigurations["ReleaseSln|Any CPU"]);
            Assert.Equal("Release2|Any CPU", library.SolutionConfigurations["ReleaseSln|Any CPU"]);
        }

        [Fact]
        public async Task TryRead_applies_a_solution_filter()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolutionFilter");
            var filterPath = Path.Combine(testProject.Directory, "ProjectAndSolutionFilter.slnf");

            Assert.True(SolutionFilterReader.TryRead(filterPath, out var solutionPath, out var projectFilter));
            Assert.True(SolutionFileReader.TryRead(solutionPath, projectFilter, out var projects));

            var project = Assert.Single(projects);
            Assert.Equal("ProjectAndSolutionFilter.csproj", Path.GetFileName(project.ProjectPath));
        }
    }
}
