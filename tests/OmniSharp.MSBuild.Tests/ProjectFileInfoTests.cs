using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.ProjectFile;
using TestUtility;
using TestUtility.Fake;
using Xunit;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectFileInfoTests
    {
        private readonly TestAssets _testAssets;
        private readonly ILogger _logger;

        public ProjectFileInfoTests()
        {
            this._testAssets = TestAssets.Instance;

            var loggerFactory = new FakeLoggerFactory();
            this._logger = loggerFactory.CreateLogger("test");

#if NET46
            var folderName = ".msbuild-net46";
#else
            var folderName = ".msbuild-netcoreapp1.0";
#endif

            var msbuildFolder = Path.Combine(this._testAssets.SolutionFolder, folderName);
            MSBuildProjectSystem.SetUpMSBuildEnvironment(msbuildFolder, this._logger);
        }

        [Fact]
        public void Hello_world_has_correct_property_values()
        {
            var projectFolder = _testAssets.GetTestProjectFolder("HelloWorld");
            var projectFilePath = Path.Combine(projectFolder, "HelloWorld.csproj");

            var projectFileInfo = ProjectFileInfo.Create(projectFilePath, projectFolder, this._logger);

            Assert.Equal(projectFilePath, projectFileInfo.ProjectFilePath);
            Assert.Equal(1, projectFileInfo.TargetFrameworks.Count);
            Assert.Equal(".NETCoreApp,Version=v1.0", projectFileInfo.TargetFrameworks[0].DotNetFrameworkName);
        }
    }
}
