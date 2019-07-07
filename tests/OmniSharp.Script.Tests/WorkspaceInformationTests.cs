using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Roslyn.CSharp.Services.Files;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Script.Tests
{
    public class WorkspaceInformationTests : AbstractTestFixture
    {
        private static Dictionary<string, string> s_netCoreScriptingConfiguration = new Dictionary<string, string>
        {
            ["script:enableScriptNuGetReferences"] = "true",
            ["script:defaultTargetFramework"] = "netcoreapp2.1"
        };

        public WorkspaceInformationTests(ITestOutputHelper output)
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
                VerifyAssemblyReference(project, "system.web");
                var commonUsingStatement = Assert.Single(project.CommonUsings);
                Assert.Equal("System.Web", commonUsingStatement);
            }
        }

        [Fact]
        public async Task CsiScriptWithFileCreatedAfterStartingServer()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("EmptyScript"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);
                Assert.Empty(workspaceInfo.Projects);

                var filePath = testProject.AddDisposableFile("main.csx");
                var service = host.GetRequestHandler<OnFilesChangedService>(OmniSharpEndpoints.FilesChanged);
                await service.Handle(new[]
                {
                    new FilesChangedRequest
                    {
                        FileName = filePath,
                        ChangeType = FileWatching.FileChangeType.Create
                    }
                });

                // back off for 2 seconds to let the watcher and workspace process new projects
                await Task.Delay(2000);

                workspaceInfo = await GetWorkspaceInfoAsync(host);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("main.csx", Path.GetFileName(project.Path));

                // should have reference to mscorlib
                VerifyCorLib(project);

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), project.GlobalsType);
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

        [Fact]
        public async Task DotnetCoreScriptSimple()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("DotnetCoreScriptSimple"))
            using (var host = CreateOmniSharpHost(testProject.Directory, configurationData: s_netCoreScriptingConfiguration))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("main.csx", Path.GetFileName(project.Path));

                // should not have reference to mscorlib
                VerifyCorLib(project, expected: false);

                // there should be multiple references to a folder of "microsoft.netcore.app"
                VerifyAssemblyReference(project, "microsoft.netcore.app");

                // there should be a reference to netstandard.dll
                VerifyAssemblyReference(project, "netstandard.dll");

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), project.GlobalsType);
            }
        }

        [Fact]
        public async Task DotnetCoreScriptWithNuget()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("DotnetCoreScriptWithNuget"))
            using (var host = CreateOmniSharpHost(testProject.Directory, configurationData: s_netCoreScriptingConfiguration))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("main.csx", Path.GetFileName(project.Path));

                // should not have reference to mscorlib
                VerifyCorLib(project, expected: false);

                // there should be multiple references to a folder of "microsoft.netcore.app"
                VerifyAssemblyReference(project, "microsoft.netcore.app");

                // there should be a reference to netstandard.dll
                VerifyAssemblyReference(project, "netstandard.dll");

                // there should be a reference to newtonsoft json
                VerifyAssemblyReference(project, "newtonsoft.json");

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), project.GlobalsType);
            }
        }

        [Fact]
        public async Task DoesntParticipateInWorkspaceInfoResponseWhenDisabled()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("SingleCsiScript"))
            using (var host = CreateOmniSharpHost(testProject.Directory, configurationData: new Dictionary<string, string>
            {
                ["script:enabled"] = "false"
            }))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);
                Assert.Null(workspaceInfo);
            }
        }

        private string GetMsCorlibPath() => Assembly.Load(new AssemblyName("mscorlib"))?.Location;

        private void VerifyCorLib(ScriptContextModel project, bool expected = true)
        {
            var corLibFound = project.AssemblyReferences.Any(r => r == GetMsCorlibPath());
            Assert.True(corLibFound == expected, $"{(expected ? "Missing" : "Unnecessary")} reference to mscorlib");
        }

        private void VerifyAssemblyReference(ScriptContextModel project, string partialName) =>
            Assert.True(project.AssemblyReferences.Any(r => r.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) > 0), $"Missing reference to {partialName}");

        private static async Task<ScriptContextModelCollection> GetWorkspaceInfoAsync(OmniSharpTestHost host)
        {
            var service = host.GetWorkspaceInformationService();

            var request = new WorkspaceInformationRequest
            {
                ExcludeSourceFiles = false
            };

            var response = await service.Handle(request);

            if (!response.ContainsKey("Script")) return null;

            return (ScriptContextModelCollection)response["Script"];
        }
    }
}
