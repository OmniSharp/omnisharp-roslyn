using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using TestUtility;
using Xunit.Abstractions;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Options;

namespace OmniSharp.MSBuild.Tests
{
    public class SolutionSdkUtilTest : AbstractMSBuildTestFixture
    {
        public SolutionSdkUtilTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task GetProjectList()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolutionSdk"))
            using (var host = CreateMSBuildTestHost(testProject.Directory))
            {
                var dic = new Dictionary<string, string>();
                var loader = new ProjectLoader(new MSBuildOptions(), testProject.Directory, dic.ToImmutableDictionary(), host.LoggerFactory, host.GetExport<SdksPathResolver>());
                var logger = LoggerFactory.CreateLogger("OmniSharp.MSBuild.Tests.SolutionSdkUtilTest");
                var sdkPathsResolver = host.GetExport<SdksPathResolver>();
                using (sdkPathsResolver.SetSdksPathEnvironmentVariable(Path.Combine(testProject.Directory, $"{testProject.Name}.csproj")))
                {
                    var lib1path = Path.Combine(testProject.Directory, "lib1", "lib1.csproj");
                    var lib2path = Path.Combine(testProject.Directory, "lib2", "lib2.csproj");
                    var projects = SolutionSdkFileUtil.GetEvaluatedProjectFilePaths(Path.Combine(testProject.Directory, "ProjectAndSolutionSdk.slnproj"), loader);
                    Assert.Contains(lib1path.Replace(testProject.Directory, "").Trim(Path.DirectorySeparatorChar), projects);
                    Assert.Contains(lib2path.Replace(testProject.Directory, "").Trim(Path.DirectorySeparatorChar), projects);
                }
            }
        }
    }
}
