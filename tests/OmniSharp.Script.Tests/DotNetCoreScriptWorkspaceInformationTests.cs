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
    public class DotNetCoreScriptWorkspaceInformationTests : WorkspaceInformationTestBase
    {
        public DotNetCoreScriptWorkspaceInformationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task DotnetCoreScriptSimple()
        {
            using (var testProject = TestAssets.Instance.GetTestScript("DotnetCoreScriptSimple"))
            using (var host = CreateOmniSharpHost(testProject.Directory, configurationData: NetCoreScriptingConfiguration))
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
            using (var host = CreateOmniSharpHost(testProject.Directory, configurationData: NetCoreScriptingConfiguration))
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
    }
}
