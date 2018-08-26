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
