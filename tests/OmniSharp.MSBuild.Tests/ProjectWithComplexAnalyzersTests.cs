using System.IO;
using System.Threading.Tasks;
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

        [Fact(Skip = "Possible thread starvation on Linux.")]
        public async Task CanLoadComplexAnalyzers()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithComplexAnalyzers"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true)))
            {
                var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));

                Assert.NotEmpty(diagnostics.QuickFixes);
                Assert.Contains(diagnostics.QuickFixes, x => x.ToString().Contains("CA1303"));
                // warning CA1303: Method 'void Program.Main(string[] args)' passes a literal string as parameter 'value' of a call to 'void Console.WriteLine(string value)'. Retrieve the following string(s) from
                // a resource table instead: "Hello World!"
            }
        }
    }
}
