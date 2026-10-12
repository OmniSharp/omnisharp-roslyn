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
    public abstract class WorkspaceInformationTestBase : AbstractTestFixture
    {
        protected static readonly Dictionary<string, string> NetCoreScriptingConfiguration = new Dictionary<string, string>
        {
            ["script:enableScriptNuGetReferences"] = "true",
            ["script:defaultTargetFramework"] = "net8.0"
        };

        protected WorkspaceInformationTestBase(ITestOutputHelper output)
            : base(output)
        {
        }

        private string GetMsCorlibPath() => Assembly.Load(new AssemblyName("mscorlib"))?.Location;

        protected void VerifyCorLib(ScriptContextModel project, bool expected = true)
        {
            var corLibFound = project.AssemblyReferences.Any(r => r == GetMsCorlibPath());
            Assert.True(corLibFound == expected, $"{(expected ? "Missing" : "Unnecessary")} reference to mscorlib");
        }

        protected void VerifyAssemblyReference(ScriptContextModel project, string partialName) =>
            Assert.True(project.AssemblyReferences.Any(r => r.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) > 0), $"Missing reference to {partialName}");

        protected static async Task<ScriptContextModelCollection> GetWorkspaceInfoAsync(OmniSharpTestHost host)
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
