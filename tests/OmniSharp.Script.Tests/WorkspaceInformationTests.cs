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
                Assert.Contains(project.ImplicitAssemblyReferences, a => a == GetMsCorlibPath());

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
                Assert.Contains(scriptProjects[0].ImplicitAssemblyReferences, a => a == GetMsCorlibPath());
                Assert.Contains(scriptProjects[1].ImplicitAssemblyReferences, a => a == GetMsCorlibPath());

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), scriptProjects[0].GlobalsType);
                Assert.Equal(typeof(CommandLineScriptGlobals), scriptProjects[1].GlobalsType);
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
