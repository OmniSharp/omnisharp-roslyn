using OmniSharp.MSBuild.ProjectFile;
using Xunit;
using MicrosoftBuildLocator = Microsoft.Build.Locator.MSBuildLocator;

namespace OmniSharp.Tests
{
    public class MSBuildContextTests
    {
        public MSBuildContextTests()
        {
            // Need to register MSBuild libraries before using them.
            if (MicrosoftBuildLocator.CanRegister)
            {
                MicrosoftBuildLocator.RegisterDefaults();
            }
        }

        [Fact]
        public void Project_path_is_case_insensitive()
        {
            var projectPath = @"c:\projects\project1\project.csproj";
            var searchProjectPath = @"c:\Projects\Project1\Project.csproj";

            var collection = new ProjectFileInfoCollection();
            collection.Add(ProjectFileInfo.CreateEmpty(projectPath));

            Assert.True(collection.TryGetValue(searchProjectPath, out var outInfo));
            Assert.NotNull(outInfo);
        }
    }
}
