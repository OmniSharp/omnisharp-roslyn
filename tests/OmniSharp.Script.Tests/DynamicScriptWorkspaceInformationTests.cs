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
    public class DynamicScriptWorkspaceInformationTests : WorkspaceInformationTestBase
    {
        public DynamicScriptWorkspaceInformationTests(ITestOutputHelper output)
            : base(output)
        {
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

                for (var attempt = 0; attempt < 40; attempt++)
                {
                    workspaceInfo = await GetWorkspaceInfoAsync(host);
                    if (workspaceInfo?.Projects.Count() == 1)
                    {
                        break;
                    }

                    await Task.Delay(50, TestContext.Current.CancellationToken);
                }
                var project = Assert.Single(workspaceInfo.Projects);

                Assert.Equal("main.csx", Path.GetFileName(project.Path));

                // should have reference to mscorlib
                VerifyCorLib(project);

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), project.GlobalsType);
            }
        }
    }
}
