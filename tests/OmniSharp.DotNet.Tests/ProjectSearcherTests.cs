using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.DotNet.Projects;
using TestUtility;
using Xunit;

namespace OmniSharp.DotNet.Tests
{
    public class ProjectSearcherTests
    {
        private readonly TestAssets _testAssets;

        public ProjectSearcherTests()
        {
            _testAssets = TestAssets.Instance;
        }

        private string GetLocation(string baseDirectory, string filePath)
        {
            var directoryPath = File.Exists(filePath)
                ? Path.GetDirectoryName(filePath)
                : filePath;

            if (directoryPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                directoryPath = directoryPath.Substring(baseDirectory.Length);
            }

            directoryPath = directoryPath.Replace(Path.DirectorySeparatorChar, '/');

            if (directoryPath.Length > 0 && directoryPath[0] == '/')
            {
                directoryPath = directoryPath.Substring(1);
            }

            return directoryPath;
        }

        [Theory]
        [InlineData("ProjectSearchSample01", "ProjectSearchSample01")]
        [InlineData("ProjectSearchSample03", "ProjectSearchSample03/src/Project1")]
        [InlineData("ProjectSearchSample04", "ProjectSearchSample04")]
        public async Task SingleResultExpect(string testSampleName, string projectName)
        {
            using (var testProject = await _testAssets.GetTestProjectAsync(testSampleName))
            {
                var projectFilePath = ProjectSearcher.Search(testProject.Directory).Single();
                Assert.Equal(projectName, GetLocation(testProject.BaseDirectory, projectFilePath));
            }
        }

        [Fact]
        public async Task NoneProjectJson()
        {
            using (var testProject = await _testAssets.GetTestProjectAsync("ProjectSearchSample02"))
            {
                var projectFilePaths = ProjectSearcher.Search(testProject.Directory);
                Assert.Empty(projectFilePaths);
            }
        }

        [Fact]
        public async Task RecursivelySearch()
        {
            using (var testProject = await _testAssets.GetTestProjectAsync("ProjectSearchSample05"))
            {
                var projectFilePaths = ProjectSearcher.Search(testProject.Directory);
                var locations = projectFilePaths.Select(p => GetLocation(testProject.BaseDirectory, p));

                var expected = new[]
                {
                    "ProjectSearchSample05/src/Project1",
                    "ProjectSearchSample05/src/Project2/Embed",
                    "ProjectSearchSample05/src/Project2",
                    "ProjectSearchSample05/test/Test01"
                };

                Assert.Equal(expected, locations);
            }
        }

        [Fact]
        public async Task GlobalJsonExpand()
        {
            using (var testProject = await _testAssets.GetTestProjectAsync("ProjectSearchSample06"))
            {
                var projectFilePaths = ProjectSearcher.Search(testProject.Directory);
                var locations = projectFilePaths.Select(p => GetLocation(testProject.BaseDirectory, p));

                var expected = new[]
                {
                    "ProjectSearchSample06/src/Project1",
                    "ProjectSearchSample06/src/Project2",
                };

                Assert.Equal(expected, locations);
            }
        }

        [Fact]
        public async Task GlobalJsonFindNothing()
        {
            using (var testProject = await _testAssets.GetTestProjectAsync("ProjectSearchSample07"))
            {
                var projectFilePaths = ProjectSearcher.Search(testProject.Directory);
                Assert.Empty(projectFilePaths);
            }
        }

        [Fact]
        public async Task GlobalJsonTopLevelFolders()
        {
            using (var testProject = await _testAssets.GetTestProjectAsync("ProjectSearchSample08"))
            {
                var projectFilePaths = ProjectSearcher.Search(testProject.Directory);
                var locations = projectFilePaths.Select(p => GetLocation(testProject.BaseDirectory, p));

                var expected = new[]
                {
                    "ProjectSearchSample08/Project1",
                    "ProjectSearchSample08/Project2",
                    "ProjectSearchSample08/Test1",
                };

                Assert.Equal(expected, locations);
            }
        }
    }
}