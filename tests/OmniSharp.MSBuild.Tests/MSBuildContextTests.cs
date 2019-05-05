using OmniSharp.MSBuild.ProjectFile;
using Xunit;

namespace OmniSharp.Tests
{
    public class MSBuildContextTests
    {
        [Fact]
        public void Project_path_is_case_insensitive()
        {
            var projectPath = @"c:\projects\project1\project.csproj";
            var searchProjectPath =  @"c:\Projects\Project1\Project.csproj";

            var collection = new ProjectFileInfoCollection();
            collection.Add(ProjectFileInfo.CreateEmpty(projectPath));

            Assert.True(collection.TryGetValue(searchProjectPath, out var outInfo));
            Assert.NotNull(outInfo);
        }
    }
}
