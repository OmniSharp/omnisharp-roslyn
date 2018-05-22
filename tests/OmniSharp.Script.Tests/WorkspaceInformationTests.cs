using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using OmniSharp.Models.WorkspaceInformation;
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
        public async Task TwoCsiScripts()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("TwoCsiScripts"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);
                var scriptProjects = workspaceInfo.Projects.ToArray();
                Assert.Equal(2, scriptProjects.Length);

                Assert.Equal("main.csx", Path.GetFileName(scriptProjects[0].Path));
                Assert.Equal("users.csx", Path.GetFileName(scriptProjects[1].Path));

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

        private string GetMsCorlibPath() => Assembly.Load(new AssemblyName("mscorlib"))?.Location;

        private void VerifyCorLib(ScriptContextModel project, bool expected = true)
        {
            var corLibFound = project.ImplicitAssemblyReferences.Any(r => r == GetMsCorlibPath());
            Assert.True(corLibFound == expected, $"{(expected ? "Missing" : "Unnecessary")} reference to mscorlib");
        }

        private void VerifyAssemblyReference(ScriptContextModel project, string partialName) =>
            Assert.True(project.ImplicitAssemblyReferences.Any(r => r.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) > 0), $"Missing reference to {partialName}");

        private static async Task<ScriptContextModelCollection> GetWorkspaceInfoAsync(OmniSharpTestHost host)
        {
            var service = host.GetWorkspaceInformationService();

            var request = new WorkspaceInformationRequest
            {
                ExcludeSourceFiles = false
            };

            var response = await service.Handle(request);

            return (ScriptContextModelCollection)response["Script"];
        }
    }
}
