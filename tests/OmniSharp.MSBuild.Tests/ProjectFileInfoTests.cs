using System;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.ProjectFile;
using TestUtility;
using TestUtility.Fake;
using Xunit;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectFileInfoTests : IDisposable
    {
        private readonly TestAssets _testAssets;
        private readonly ILogger _logger;

        private const string MSBUILD_EXE_PATH = "MSBUILD_EXE_PATH";
        private readonly bool _isMSBuildExePathSet;
        private readonly string _previousMSBuildExePath;

        public ProjectFileInfoTests()
        {
            this._testAssets = TestAssets.Instance;

            var loggerFactory = new FakeLoggerFactory();
            this._logger = loggerFactory.CreateLogger("test");

            var msbuildExePath = Path.Combine(Directory.GetCurrentDirectory(), "msbuild.exe");
            if (File.Exists(msbuildExePath))
            {
                this._previousMSBuildExePath = Environment.GetEnvironmentVariable(MSBUILD_EXE_PATH);
                Environment.SetEnvironmentVariable(MSBUILD_EXE_PATH, msbuildExePath);
                this._isMSBuildExePathSet = true;
            }
        }

        public void Dispose()
        {
            if (this._isMSBuildExePathSet)
            {
                Environment.SetEnvironmentVariable(MSBUILD_EXE_PATH, this._previousMSBuildExePath);
            }
        }

        [Fact(Skip = "Doesn't run on OSX/Linux yet")]
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
