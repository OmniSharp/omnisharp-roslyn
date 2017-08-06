using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectFileInfoTests : AbstractTestFixture
    {
        private readonly TestAssets _testAssets;
        private readonly ILogger _logger;

        public ProjectFileInfoTests(ITestOutputHelper output)
            : base(output)
        {
            this._testAssets = TestAssets.Instance;
            this._logger = this.LoggerFactory.CreateLogger<ProjectFileInfoTests>();

            if (!MSBuildEnvironment.IsInitialized)
            {
                MSBuildEnvironment.Initialize(this._logger);
            }
        }

        private static string GetSdksPath(OmniSharpTestHost host)
        {
            var dotNetCli = host.GetExport<DotNetCliService>();
            var info = dotNetCli.GetInfo();

            return Path.Combine(info.BasePath, "Sdks");
        }

        [Fact]
        public async Task HelloWorld_has_correct_property_values()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("HelloWorld"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "HelloWorld.csproj");

                var projectFileInfo = ProjectFileInfo.Create(projectFilePath, testProject.Directory, GetSdksPath(host), this._logger);

                Assert.NotNull(projectFileInfo);
                Assert.Equal(projectFilePath, projectFileInfo.FilePath);
                Assert.Equal(1, projectFileInfo.TargetFrameworks.Length);
                Assert.Equal("netcoreapp1.0", projectFileInfo.TargetFrameworks[0]);
                Assert.Equal("bin/Debug/netcoreapp1.0/", projectFileInfo.OutputPath.Replace('\\', '/'));
                Assert.Equal(3, projectFileInfo.SourceFiles.Length); // Program.cs, AssemblyInfo.cs, AssemblyAttributes.cs
            }
        }

        [Fact]
        public async Task HelloWorldSlim_has_correct_property_values()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("HelloWorldSlim"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "HelloWorldSlim.csproj");

                var projectFileInfo = ProjectFileInfo.Create(projectFilePath, testProject.Directory, GetSdksPath(host), this._logger);

                Assert.NotNull(projectFileInfo);
                Assert.Equal(projectFilePath, projectFileInfo.FilePath);
                Assert.Equal(1, projectFileInfo.TargetFrameworks.Length);
                Assert.Equal("netcoreapp1.0", projectFileInfo.TargetFrameworks[0]);
                Assert.Equal("bin/Debug/netcoreapp1.0/", projectFileInfo.OutputPath.Replace('\\', '/'));
                Assert.Equal(3, projectFileInfo.SourceFiles.Length); // Program.cs, AssemblyInfo.cs, AssemblyAttributes.cs
            }
        }

        [Fact]
        public async Task NetStandardAndNetCoreApp_has_correct_property_values()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("NetStandardAndNetCoreApp"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "NetStandardAndNetCoreApp.csproj");

                var projectFileInfo = ProjectFileInfo.Create(projectFilePath, testProject.Directory, GetSdksPath(host), this._logger);

                Assert.NotNull(projectFileInfo);
                Assert.Equal(projectFilePath, projectFileInfo.FilePath);
                Assert.Equal(2, projectFileInfo.TargetFrameworks.Length);
                Assert.Equal("netcoreapp1.0", projectFileInfo.TargetFrameworks[0]);
                Assert.Equal("netstandard1.5", projectFileInfo.TargetFrameworks[1]);
                Assert.Equal(@"bin/Debug/netcoreapp1.0/", projectFileInfo.OutputPath.Replace('\\', '/'));
                Assert.Equal(3, projectFileInfo.SourceFiles.Length); // Program.cs, AssemblyInfo.cs, AssemblyAttributes.cs
            }
        }
    }
}
