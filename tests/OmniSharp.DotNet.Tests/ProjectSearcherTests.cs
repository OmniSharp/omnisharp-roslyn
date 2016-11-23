using System;
using System.IO;
using System.Linq;
using OmniSharp.DotNet.Projects;
using TestCommon;
using Xunit;

namespace OmniSharp.DotNet.Tests
{
    public class ProjectSearcherTests
    {
        private readonly TestsContext _context;

        public ProjectSearcherTests()
        {
            _context = TestsContext.Default;
        }

        private string GetLocation(string filePath)
        {
            var directoryPath = File.Exists(filePath)
                ? Path.GetDirectoryName(filePath)
                : filePath;

            if (directoryPath.StartsWith(_context.TestSamples, StringComparison.OrdinalIgnoreCase))
            {
                directoryPath = directoryPath.Substring(_context.TestSamples.Length);
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
            var directory = _context.GetTestSample(testSampleName);
            var projectFilePath = ProjectSearcher.Search(directory).Single();

            Assert.Equal(projectName, GetLocation(projectFilePath));
        }

        [Fact]
        public void NoneProjectJson()
        {
            var directory = _context.GetTestSample("ProjectSearchSample02");
            var projectFilePaths = ProjectSearcher.Search(directory);

            Assert.Empty(projectFilePaths);
        }

        [Fact]
        public void RecursivelySearch()
        {
            var directory = _context.GetTestSample("ProjectSearchSample05");
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
            var directory = _context.GetTestSample("ProjectSearchSample06");
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
            var directory = _context.GetTestSample("ProjectSearchSample07");
            var projectFilePaths = ProjectSearcher.Search(directory);

            Assert.Empty(projectFilePaths);
        }

        [Fact]
        public void GlobalJsonTopLevelFolders()
        {
            var directory = _context.GetTestSample("ProjectSearchSample08");
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