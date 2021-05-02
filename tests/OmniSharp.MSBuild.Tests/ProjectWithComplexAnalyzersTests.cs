
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.Diagnostics;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectWithComplexAnalyzersTests : AbstractMSBuildTestFixture
    {
        public ProjectWithComplexAnalyzersTests(ITestOutputHelper output) : base(output)
        {
        }

        // possible thread starvation on *nix when running in Azure DevOps
        [ConditionalFact(typeof(WindowsOnly))]
        public async Task CanLoadComplexAnalyzers()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithComplexAnalyzers"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true)))
            {
                var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));

                Assert.NotEmpty(diagnostics.QuickFixes);
                Assert.Contains(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "CA1303");
                // warning CA1303: Method 'void Program.Main(string[] args)' passes a literal string as parameter 'value' of a call to 'void Console.WriteLine(string value)'. Retrieve the following string(s) from
                // a resource table instead: "Hello World!"
            }
        }
    }
}
