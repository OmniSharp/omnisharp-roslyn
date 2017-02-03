using System;
using System.IO;
using System.Linq;
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

        private string GetLocation(string filePath)
        {
            var directoryPath = File.Exists(filePath)
                ? Path.GetDirectoryName(filePath)
                : filePath;

            if (directoryPath.StartsWith(_testAssets.TestProjectsFolder, StringComparison.OrdinalIgnoreCase))
            {
                directoryPath = directoryPath.Substring(_testAssets.TestProjectsFolder.Length);
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
        public void SingleResultExpect(string testSampleName, string projectName)
        {
            var directory = _testAssets.GetTestProjectFolder(testSampleName);
            var projectFilePath = ProjectSearcher.Search(directory).Single();

            Assert.Equal(projectName, GetLocation(projectFilePath));
        }

        [Fact]
        public void NoneProjectJson()
        {
            var directory = _testAssets.GetTestProjectFolder("ProjectSearchSample02");
            var projectFilePaths = ProjectSearcher.Search(directory);

            Assert.Empty(projectFilePaths);
        }

        [Fact]
        public void RecursivelySearch()
        {
            var directory = _testAssets.GetTestProjectFolder("ProjectSearchSample05");
            var projectFilePaths = ProjectSearcher.Search(directory);
            var locations = projectFilePaths.Select(GetLocation);

            var expected = new []
            {
                "ProjectSearchSample05/src/Project1",
                "ProjectSearchSample05/src/Project2/Embed",
                "ProjectSearchSample05/src/Project2",
                "ProjectSearchSample05/test/Test01"
            };
            
            Assert.Equal(expected, locations);
        }
        
        [Fact]
        public void GlobalJsonExpand()
        {
            var directory = _testAssets.GetTestProjectFolder("ProjectSearchSample06");
            var projectFilePaths = ProjectSearcher.Search(directory);
            var locations = projectFilePaths.Select(GetLocation);

            var expected = new []
            {
                "ProjectSearchSample06/src/Project1",
                "ProjectSearchSample06/src/Project2",
            };

            Assert.Equal(expected, locations);
        }

        [Fact]
        public void GlobalJsonFindNothing()
        {
            var directory = _testAssets.GetTestProjectFolder("ProjectSearchSample07");
            var projectFilePaths = ProjectSearcher.Search(directory);

            Assert.Empty(projectFilePaths);
        }

        [Fact]
        public void GlobalJsonTopLevelFolders()
        {
            var directory = _testAssets.GetTestProjectFolder("ProjectSearchSample08");
            var projectFilePaths = ProjectSearcher.Search(directory);
            var locations = projectFilePaths.Select(GetLocation);

            var expected = new []
            {
                "ProjectSearchSample08/Project1",
                "ProjectSearchSample08/Project2",
                "ProjectSearchSample08/Test1",
            };

            Assert.Equal(expected, locations);
        }
    }
}