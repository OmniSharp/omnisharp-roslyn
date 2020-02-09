using Microsoft.Extensions.Logging;
using OmniSharp.FileSystem;
using OmniSharp.Options;
using OmniSharp.Services;
using System.Linq;
using TestUtility;
using Xunit;

namespace OmniSharp.Tests
{
    public class FileSystemHelperFacts
    {
        [Fact]
        public void FileSystemHelperFacts_CanExcludeSearchPath_File()
        {
            var helper = CreateFileSystemHelper("**/ProjectWithSdkProperty.csproj");

            var msbuildProjectFiles = helper.GetFiles("**/*.csproj");
            Assert.NotEmpty(msbuildProjectFiles);

            var projectWithSdkProperty = msbuildProjectFiles.FirstOrDefault(p => p.Contains("ProjectWithSdkProperty"));
            Assert.Null(projectWithSdkProperty);
        }

        [Fact]
        public void FileSystemHelperFacts_CanExcludeSearchPath_MultipleFiles()
        {
            var helper = CreateFileSystemHelper("**/MSTestProject.csproj", "**/NUnitTestProject.csproj");

            var msbuildProjectFiles = helper.GetFiles("**/*.csproj");
            Assert.NotEmpty(msbuildProjectFiles);

            var msTestProject = msbuildProjectFiles.FirstOrDefault(p => p.Contains("MSTestProject"));
            Assert.Null(msTestProject);

            var nunitTestProject = msbuildProjectFiles.FirstOrDefault(p => p.Contains("NUnitTestProject"));
            Assert.Null(nunitTestProject);
        }

        [Fact]
        public void FileSystemHelperFacts_CanExcludeSearchPath_Folder()
        {
            var helper = CreateFileSystemHelper("**/ProjectWithSdkProperty/**/*");

            var msbuildProjectFiles = helper.GetFiles("**/*.csproj");
            Assert.NotEmpty(msbuildProjectFiles);

            var projectWithSdkProperty = msbuildProjectFiles.FirstOrDefault(p => p.Contains("ProjectWithSdkProperty"));
            Assert.Null(projectWithSdkProperty);
        }

        [Fact]
        public void FileSystemHelperFacts_CanExcludeSearchPath_MultipleFolders()
        {
            var helper = CreateFileSystemHelper("**/MSTestProject/**/*", "**/NUnitTestProject/**/*");

            var msbuildProjectFiles = helper.GetFiles("**/*.csproj");
            Assert.NotEmpty(msbuildProjectFiles);

            var msTestProject = msbuildProjectFiles.FirstOrDefault(p => p.Contains("MSTestProject"));
            Assert.Null(msTestProject);

            var nunitTestProject = msbuildProjectFiles.FirstOrDefault(p => p.Contains("NUnitTestProject"));
            Assert.Null(nunitTestProject);
        }

        [Fact]
        public void FileSystemHelperFacts_CanExcludeSearchPath_MultipleFolders_BothSystemAndUserPaths()
        {
            var helper = CreateFileSystemHelper(new[] { "**/MSTestProject/**/*", "**/NUnitTestProject/**/*" }, systemExcludePatterns: new[] { "**/ProjectWithSdkProperty/**/*" });

            var msbuildProjectFiles = helper.GetFiles("**/*.csproj");
            Assert.NotEmpty(msbuildProjectFiles);

            var msTestProject = msbuildProjectFiles.FirstOrDefault(p => p.Contains("MSTestProject"));
            Assert.Null(msTestProject);

            var nunitTestProject = msbuildProjectFiles.FirstOrDefault(p => p.Contains("NUnitTestProject"));
            Assert.Null(nunitTestProject);

            var projectWithSdkProperty = msbuildProjectFiles.FirstOrDefault(p => p.Contains("ProjectWithSdkProperty"));
            Assert.Null(projectWithSdkProperty);
        }

        [Fact]
        public void FileSystemHelperFacts_CanHandleInvalidPath()
        {
            var helper = CreateFileSystemHelper("!@@#$$@%&&*()_+");

            var ex = Record.Exception(() => helper.GetFiles("**/*.csproj"));
            Assert.Null(ex);
        }

        [Fact]
        public void IsChildPath_CheckThatPathIsChildPath()
        {
            const string path = "/src/project/test.csproj";
            const string root = "/src/project";

            bool result = FileSystemHelper.IsChildPath(path, root);

            Assert.True(result);
        }

        [Fact]
        public void IsChildPath_CheckThatIsNotChildPath_WithShortPath()
        {
            const string path = "/src";
            const string root = "/src/project";

            bool result = FileSystemHelper.IsChildPath(path, root);

            Assert.False(result);
        }

        [Fact]
        public void IsChildPath_CheckThatIsNotChildPath_WithDifferentPaths()
        {
            const string path = "/src/project/file.cs";
            const string root = "/src/other-project";

            bool result = FileSystemHelper.IsChildPath(path, root);

            Assert.False(result);
        }

        [Fact]
        public void IsChildPath_CheckThatIsNotChildPath_WithAnyOfThePathsNull()
        {
            bool result = FileSystemHelper.IsChildPath(null, null);

            Assert.False(result);
        }

        [Fact]
        public void FindParentPath()
        {
            const string expectedPath = "/src/project";

            const string path = "/src/project/file.cs";
            var candidateParentPaths = new[]
            {
                "/src/project/obj",
                expectedPath,
                "/test/project.test",
            };

            string result = FileSystemHelper.FindParentPath(path, candidateParentPaths);

            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void FindParentPath_ReturnsNullIfNoParentFound()
        {
            const string path = "/src/project/";
            var candidateParentPaths = new[] { "/test/project" };

            string result = FileSystemHelper.FindParentPath(path, candidateParentPaths);

            Assert.Null(result);
        }

        [Fact]
        public void FindParentPath_ReturnsNullIfPathOrParentPathsIsNull()
        {
            string result = FileSystemHelper.FindParentPath(null, null);

            Assert.Null(result);
        }

        [Fact]
        public void FindParentPath_GetParentPath_GivenCandidatePathsAsFile()
        {
            const string expectedPath = "/src/project/project.csproj";
            const string path = "/src/project/file.cs";
            var candidateParentPaths = new[]
            {
                "/src/project/obj",
                "/src/project/obj/",
                expectedPath,
            };

            string result = FileSystemHelper.FindParentPath(path, candidateParentPaths);

            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void IsFileExcluded_VerifyThatFileIsNotExcluded_IfNotChildOfTargetPath()
        {
            const string file = "/src/project/excluded/file.cs";
            const string targetDirectory = "/test/project";
            var excludePatterns = new[] { "**/excluded" };

            bool result = FileSystemHelper.IsPathExcluded(file, targetDirectory, excludePatterns);

            Assert.False(result);
        }

        [Theory]
        [InlineData("/src/project")]
        [InlineData("/src/project/")]
        public void IsFileExcluded_VerifyThatFileIsExcluded(string targetDirectory)
        {
            const string file = "/src/project/excluded/file.cs";
            var excludePatterns = new[] { "**/excluded" };

            bool result = FileSystemHelper.IsPathExcluded(file, targetDirectory, excludePatterns);

            Assert.True(result);
        }

        private FileSystemHelper CreateFileSystemHelper(params string[] excludePatterns)
        {
            var environment = new OmniSharpEnvironment(TestAssets.Instance.TestAssetsFolder, 1000, LogLevel.Information, null);
            var options = new OmniSharpOptions();
            options.FileOptions.ExcludeSearchPatterns = excludePatterns;
            var helper = new FileSystemHelper(options, environment);
            return helper;
        }

        private FileSystemHelper CreateFileSystemHelper(string[] excludePatterns, string[] systemExcludePatterns)
        {
            var environment = new OmniSharpEnvironment(TestAssets.Instance.TestAssetsFolder, 1000, LogLevel.Information, null);
            var options = new OmniSharpOptions();
            options.FileOptions.ExcludeSearchPatterns = excludePatterns;
            options.FileOptions.SystemExcludeSearchPatterns = systemExcludePatterns;

            var helper = new FileSystemHelper(options, environment);
            return helper;
        }
    }
}
