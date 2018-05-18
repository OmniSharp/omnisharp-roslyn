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
                Assert.Contains(project.ImplicitAssemblyReferences, a => a == Assembly.Load(new AssemblyName("mscorlib"))?.Location);

                // default globals object
                Assert.Equal(typeof(CommandLineScriptGlobals), project.GlobalsType);
            }
        }

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
