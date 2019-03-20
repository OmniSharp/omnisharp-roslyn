using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class DiagnosticsFacts
    {
        private readonly ITestOutputHelper _testOutput;

        public DiagnosticsFacts(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CodeCheckSpecifiedFileOnly(bool roslynAnalyzersEnabled)
        {
            using (var host = GetHost(roslynAnalyzersEnabled))
            {
                host.AddFilesToWorkspace(new TestFile("a.cs", "class C { int n = true; }"));
                var quickFixes = await host.RequestCodeCheckAsync("a.cs");

                Assert.Contains(quickFixes.QuickFixes.Select(x => x.ToString()), x => x.Contains("CS0029"));
                Assert.Equal("a.cs", quickFixes.QuickFixes.First().FileName);
            }
        }

        private OmniSharpTestHost GetHost(bool roslynAnalyzersEnabled)
        {
            return OmniSharpTestHost.Create(testOutput: _testOutput, configurationData: new Dictionary<string, string>() { { "RoslynExtensionsOptions:EnableAnalyzersSupport", roslynAnalyzersEnabled.ToString() } });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CheckAllFiles(bool roslynAnalyzersEnabled)
        {
            using (var host = GetHost(roslynAnalyzersEnabled))
            {
                host.AddFilesToWorkspace(
                    new TestFile("a.cs", "class C1 { int n = true; }"),
                    new TestFile("b.cs", "class C2 { int n = true; }"));

                var quickFixes = await host.RequestCodeCheckAsync();

                Assert.Contains(quickFixes.QuickFixes, x => x.Text.Contains("CS0029") && x.FileName == "a.cs");
                Assert.Contains(quickFixes.QuickFixes, x => x.Text.Contains("CS0029") && x.FileName == "b.cs");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WhenFileIsDeletedFromWorkSpaceThenResultsAreNotReturnedAnyMore(bool roslynAnalyzersEnabled)
        {
            using (var host = GetHost(roslynAnalyzersEnabled))
            {
                host.AddFilesToWorkspace(new TestFile("a.cs", "class C1 { int n = true; }"));
                await host.RequestCodeCheckAsync();

                foreach (var doc in host.Workspace.CurrentSolution.Projects.SelectMany(x => x.Documents))
                {
                    // Theres document for each targeted framework, lets delete all.
                    host.Workspace.RemoveDocument(doc.Id);
                }

                var quickFixes = await host.RequestCodeCheckAsync();

                Assert.DoesNotContain(quickFixes.QuickFixes, x => x.Text.Contains("CS0029") && x.FileName == "a.cs");
            }
        }

        [Fact]
        public async Task AnalysisSupportBuiltInIDEAnalysers()
        {
            using (var host = GetHost(roslynAnalyzersEnabled: true))
            {
                host.AddFilesToWorkspace(
                    new TestFile("a.cs", "class C1 { int n = true; }"));

                var quickFixes = await host.RequestCodeCheckAsync("a.cs");
                Assert.Contains(quickFixes.QuickFixes, x => x.Text.Contains("IDE0044"));
            }
        }

        [Fact]
        public async Task WhenUnusedImportExistsWithoutAnalyzersEnabled_ThenReturnEmptyTags()
        {
            using (var host = GetHost(roslynAnalyzersEnabled: false))
            {
                host.AddFilesToWorkspace(
                    new TestFile("returnemptytags.cs", @"using System.IO;"));

                var quickFixResponse = await host.RequestCodeCheckAsync("returnemptytags.cs");

                var allDiagnostics = quickFixResponse.QuickFixes.OfType<DiagnosticLocation>();

                Assert.Empty(allDiagnostics.SelectMany(x => x.Tags));
            }
        }

        [Fact]
        public async Task WhenUnusedImportIsFoundAndAnalyzersEnabled_ThenReturnUnnecessaryTag()
        {
            using (var host = GetHost(roslynAnalyzersEnabled: true))
            {
                host.AddFilesToWorkspace(
                    new TestFile("returnidetags.cs", @"using System.IO;"));

                var quickFixResponse = await host.RequestCodeCheckAsync("returnidetags.cs");

                Assert.Contains("Unnecessary", quickFixResponse
                    .QuickFixes
                    .OfType<DiagnosticLocation>()
                    .Single(x => x.Id == "IDE0005")
                    .Tags);
            }
        }
    }
}
