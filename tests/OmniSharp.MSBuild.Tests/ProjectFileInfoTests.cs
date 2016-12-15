using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.ProjectFile;
using TestUtility;
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

            MSBuildEnvironment.Initialize(this._logger);
        }

        [ConditionalFact(typeof(NotOnAppVeyor))]
        public void HelloWorld_has_correct_property_values()
        {
            var projectFolder = _testAssets.GetTestProjectFolder("HelloWorld");
            var projectFilePath = Path.Combine(projectFolder, "HelloWorld.csproj");

            var projectFileInfo = ProjectFileInfo.Create(projectFilePath, projectFolder, this._logger);

            Assert.NotNull(projectFileInfo);
            Assert.Equal(projectFilePath, projectFileInfo.ProjectFilePath);
            Assert.Equal(1, projectFileInfo.TargetFrameworks.Count);
            Assert.Equal("netcoreapp1.0", projectFileInfo.TargetFrameworks[0]);
            Assert.Equal("bin/Debug/netcoreapp1.0/", projectFileInfo.OutputPath.Replace('\\', '/'));
        }

        [ConditionalFact(typeof(NotOnAppVeyor))]
        public void NetStandardAndNetCoreApp_has_correct_property_values()
        {
            var projectFolder = _testAssets.GetTestProjectFolder("NetStandardAndNetCoreApp");
            var projectFilePath = Path.Combine(projectFolder, "NetStandardAndNetCoreApp.csproj");

            var projectFileInfo = ProjectFileInfo.Create(projectFilePath, projectFolder, this._logger);

            Assert.NotNull(projectFileInfo);
            Assert.Equal(projectFilePath, projectFileInfo.ProjectFilePath);
            Assert.Equal(2, projectFileInfo.TargetFrameworks.Count);
            Assert.Equal("netcoreapp1.0", projectFileInfo.TargetFrameworks[0]);
            Assert.Equal("netstandard1.5", projectFileInfo.TargetFrameworks[1]);
            Assert.Equal(@"bin/Debug/netcoreapp1.0/", projectFileInfo.OutputPath.Replace('\\', '/'));
        }
    }
}
