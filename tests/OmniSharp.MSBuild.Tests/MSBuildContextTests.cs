using OmniSharp.MSBuild;
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

            MSBuildContext context = new MSBuildContext();
            context.Projects.Add(projectPath, new ProjectFileInfo());

            ProjectFileInfo outInfo = null;
            Assert.True(context.Projects.TryGetValue(searchProjectPath, out outInfo ));
            Assert.NotNull(outInfo);
        }
    }
}