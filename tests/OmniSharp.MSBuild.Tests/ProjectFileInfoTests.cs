using System;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.ProjectFile;
using TestUtility;
using TestUtility.Fake;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectFileInfoTests
    {
        private readonly TestAssets _testAssets;
        private readonly ILogger _logger;

        public ProjectFileInfoTests(ITestOutputHelper output)
        {
            this._testAssets = TestAssets.Instance;
            this._logger = new TestLogger(output);

            MSBuildProjectSystem.SetUpMSBuildEnvironment(this._logger);
        }

        [Fact(Skip = "Won't work until we restore .NET Core .csproj projects")]
        public void Hello_world_has_correct_property_values()
        {
            var projectFolder = _testAssets.GetTestProjectFolder("HelloWorld");
            var projectFilePath = Path.Combine(projectFolder, "HelloWorld.csproj");

            var projectFileInfo = ProjectFileInfo.Create(projectFilePath, projectFolder, this._logger);

            Assert.NotNull(projectFileInfo);
            Assert.Equal(projectFilePath, projectFileInfo.ProjectFilePath);
            Assert.Equal(1, projectFileInfo.TargetFrameworks.Count);
            Assert.Equal(".NETCoreApp,Version=v1.0", projectFileInfo.TargetFrameworks[0].DotNetFrameworkName);
        }
    }
}
