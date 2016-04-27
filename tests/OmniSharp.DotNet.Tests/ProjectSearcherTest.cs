using System.IO;
using System.Linq;
using OmniSharp.DotNet.Projects;
using TestCommon;
using Xunit;

namespace OmniSharp.DotNet.Tests
{
    public class ProjectSearcherTest
    {
        private readonly TestsContext _context;

        public ProjectSearcherTest()
        {
            _context = TestsContext.Default;
        }

        [Theory]
        [InlineData("ProjectSearchSample01", "ProjectSearchSample01")]
        [InlineData("ProjectSearchSample03", "Project1")]
        [InlineData("ProjectSearchSample04", "ProjectSearchSample04")]
        public void SingleResultExpect(string testSampleName, string projectName)
        {
            var projectPath = _context.GetTestSample(testSampleName);
            var project = ProjectSearcher.Search(projectPath).Single();

            Assert.Equal(projectName, Path.GetFileName(project));
        }

        [Fact]
        public void NoneProjectJson()
        {
            var projectPath = _context.GetTestSample("ProjectSearchSample02");
            Assert.Empty(ProjectSearcher.Search(projectPath));
        }

        [Fact]
        public void RecursivelySearch()
        {
            var projectPath = _context.GetTestSample("ProjectSearchSample05");
            var results = ProjectSearcher.Search(projectPath);
            
            Assert.Equal(3, results.Count());
        }
        
        [Fact]
        public void GlobalJsonExpand()
        {
            var projectPath = _context.GetTestSample("ProjectSearchSample06");
            var results = ProjectSearcher.Search(projectPath);
            
            Assert.Equal(2, results.Count());
        }
        
        [Fact]
        public void GlobalJsonFindNothing()
        {
            var projectPath = _context.GetTestSample("ProjectSearchSample07");
            Assert.Empty(ProjectSearcher.Search(projectPath));
        }
    }
}