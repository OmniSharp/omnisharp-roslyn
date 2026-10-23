using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Roslyn.CSharp.Services.Files;
using TestUtility;
using Xunit;

namespace OmniSharp.Script.Tests
{
    public class CsiWorkspaceInformationTests : WorkspaceInformationTestBase
    {
        public CsiWorkspaceInformationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task SingleCsiScript()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("SingleCsiScript"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("main.csx", Path.GetFileName(project.Path));

                // should have reference to mscorlib
                VerifyCorLib(project);

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), project.GlobalsType);
            }
        }

        [Fact]
        public async Task SingleCsiScriptWithCustomRspNamespacesAndReferences()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("SingleCsiScriptWithCustomRsp"))
            using (var host = CreateOmniSharpHost(testProject.Directory, new Dictionary<string, string>
            {
                ["script:rspFilePath"] = Path.Combine(testProject.Directory, "test.rsp")
            }))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("main.csx", Path.GetFileName(project.Path));

                // should have reference to mscorlib
                VerifyCorLib(project);

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), project.GlobalsType);

                // should have RSP inherited settings
                VerifyAssemblyReference(project, "system.xml");
                var commonUsingStatement = Assert.Single(project.CommonUsings);
                Assert.Equal("System.Xml", commonUsingStatement);
            }
        }

        [Fact]
        public async Task TwoCsiScripts()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("TwoCsiScripts"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);
                var scriptProjects = workspaceInfo.Projects.ToArray();
                Assert.Equal(2, scriptProjects.Length);

                // ordering is non deterministic
                Assert.True(scriptProjects.Any(x => Path.GetFileName(x.Path) == "main.csx"), "Expected a 'main.csx' but couldn't find it");
                Assert.True(scriptProjects.Any(x => Path.GetFileName(x.Path) == "users.csx"), "Expected a 'main.csx' but couldn't find it");

                // should have reference to mscorlib
                VerifyCorLib(scriptProjects[0]);
                VerifyCorLib(scriptProjects[1]);

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), scriptProjects[0].GlobalsType);
                Assert.Equal(typeof(CommandLineScriptGlobals), scriptProjects[1].GlobalsType);
            }
        }
    }
}
