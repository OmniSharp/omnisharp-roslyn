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
                var scriptProjects = workspaceInfo.Projects.ToArray();
                Assert.Single(scriptProjects);

                var project = scriptProjects[0];
                Assert.Equal("main.csx", Path.GetFileName(project.Path));

                // should have reference to mscorlib
                Assert.True(project.ImplicitAssemblyReferences.Any(r => r == GetMsCorlibPath()), "Missing reference to mscorlib");

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
                Assert.True(scriptProjects[0].ImplicitAssemblyReferences.Any(r => r == GetMsCorlibPath()), "Missing reference to mscorlib");
                Assert.True(scriptProjects[1].ImplicitAssemblyReferences.Any(r => r == GetMsCorlibPath()), "Missing reference to mscorlib");

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), scriptProjects[0].GlobalsType);
                Assert.Equal(typeof(CommandLineScriptGlobals), scriptProjects[1].GlobalsType);
            }
        }

        [Fact]
        public async Task DotnetCoreScriptSimple()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("DotnetCoreScriptSimple"))
            using (var host = CreateOmniSharpHost(testProject.Directory, configurationData: new Dictionary<string, string>
            {
                { "script:enableScriptNuGetReferences", "true" },
                { "script:defaultTargetFramework", "netcoreapp2.1" }
            }))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);
                var scriptProjects = workspaceInfo.Projects.ToArray();
                Assert.Single(scriptProjects);

                var project = scriptProjects[0];
                Assert.Equal("main.csx", Path.GetFileName(project.Path));

                // should not have reference to mscorlib
                Assert.False(project.ImplicitAssemblyReferences.Any(r => r == GetMsCorlibPath()), "Unnecessary reference to mscorlib");

                // there should be multiple references to a folder of "microsoft.netcore.app"
                Assert.True(project.ImplicitAssemblyReferences.Any(r => r.IndexOf("microsoft.netcore.app", StringComparison.OrdinalIgnoreCase) > 0), "Missing reference to microsoft.netcore.app");

                // there should be a reference to netstandard.dll
                Assert.True(project.ImplicitAssemblyReferences.Any(r => r.IndexOf("netstandard.dll", StringComparison.OrdinalIgnoreCase) > 0), "Missing reference to a NET Standard dll");

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), project.GlobalsType);
            }
        }

        [Fact]
        public async Task DotnetCoreScriptWithNuget()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("DotnetCoreScriptWithNuget"))
            using (var host = CreateOmniSharpHost(testProject.Directory, configurationData: new Dictionary<string, string>
            {
                { "script:enableScriptNuGetReferences", "true" },
                { "script:defaultTargetFramework", "netcoreapp2.1" }
            }))
            {
                var workspaceInfo = await GetWorkspaceInfoAsync(host);
                var scriptProjects = workspaceInfo.Projects.ToArray();
                Assert.Single(scriptProjects);

                var project = scriptProjects[0];
                Assert.Equal("main.csx", Path.GetFileName(project.Path));

                // should not have reference to mscorlib
                Assert.False(project.ImplicitAssemblyReferences.Any(r => r == GetMsCorlibPath()), "Unnecessary reference to mscorlib");

                // there should be multiple references to a folder of "microsoft.netcore.app"
                Assert.True(project.ImplicitAssemblyReferences.Any(r => r.IndexOf("microsoft.netcore.app", StringComparison.OrdinalIgnoreCase) > 0), "Missing reference to microsoft.netcore.app");

                // there should be a reference to netstandard.dll
                Assert.True(project.ImplicitAssemblyReferences.Any(r => r.IndexOf("netstandard.dll", StringComparison.OrdinalIgnoreCase) > 0), "Missing reference to a NET Standard dll");

                // there should be a reference to newtonsoft json
                Assert.True(project.ImplicitAssemblyReferences.Any(r => r.IndexOf("newtonsoft.json", StringComparison.OrdinalIgnoreCase) > 0), "Missing reference to a Newtonsoft Json");

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), project.GlobalsType);
            }
        }

        private string GetMsCorlibPath() => Assembly.Load(new AssemblyName("mscorlib"))?.Location;

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
