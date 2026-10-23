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
    public class DisabledScriptWorkspaceInformationTests : WorkspaceInformationTestBase
    {
        public DisabledScriptWorkspaceInformationTests(ITestOutputHelper output)
            : base(output)
        {
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
    }
}
