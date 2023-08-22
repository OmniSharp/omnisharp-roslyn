using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models.Diagnostics;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Lsp.Tests
{
    public class OmnisharpPublishDiagnosticFacts : AbstractLanguageServerTestBase
    {
        public OmnisharpPublishDiagnosticFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CodeCheckSpecifiedFileOnly(bool roslynAnalyzersEnabled)
        {
            await ReadyHost(roslynAnalyzersEnabled);
            var testFile = new TestFile("a.cs", "class C { int n = true; }");
            AddFilesToWorkspace(testFile);
            await OpenFile(testFile.FileName);

            await WaitForDiagnostics();

            var quickFixes = GetDiagnostics("a.cs");
            Assert.Contains(quickFixes, x => x.Code == "CS0029");
        }

        private Task ReadyHost(bool roslynAnalyzersEnabled)
        {
            return Restart(configurationData: new Dictionary<string, string>
                {{"RoslynExtensionsOptions:EnableAnalyzersSupport", roslynAnalyzersEnabled.ToString()}});
        }

        [Fact]
        public async Task CheckAllFilesWithAnalyzersWillEventuallyReturnAllResults()
        {
            await ReadyHost(true);
            AddFilesToWorkspace(
                new TestFile("a.cs", "class C1 { int n = true; }"),
                new TestFile("b.cs", "class C2 { int n = true; }"));

            await WaitForDiagnostics();

            Assert.Contains(GetDiagnostics("a.cs"),
                x => x.Code == "CS0029");
            Assert.Contains(GetDiagnostics("b.cs"),
                x => x.Code == "CS0029");
        }

        [Fact]
        public async Task AnalysisSupportBuiltInIDEAnalysers()
        {
            await ReadyHost(true);
            AddFilesToWorkspace(new TestFile("a.cs", "class C1 { int n = true; }"));

            await WaitForDiagnostics();
            Assert.Contains(GetDiagnostics("a.cs"), x => x.Code == "IDE0044");
        }

        [Fact]
        public async Task WhenUnusedImportExistsWithoutAnalyzersEnabled_ThenReturnEmptyTags()
        {
            await ReadyHost(false);
            AddFilesToWorkspace(
                new TestFile("returnemptytags.cs", @"using System.IO;"));

            await WaitForDiagnostics();

            var allDiagnostics = GetDiagnostics("returnemptytags.cs");

            Assert.Empty(allDiagnostics.SelectMany(x => x.Tags));
        }

        [Fact]
        public async Task WhenUnusedImportIsFoundAndAnalyzersEnabled_ThenReturnUnnecessaryTag()
        {
            await ReadyHost(true);
            AddFilesToWorkspace(
                new TestFile("returnidetags.cs", @"using System.IO;"));

            await WaitForDiagnostics();

            Assert.Contains(DiagnosticTag.Unnecessary,
                GetDiagnostics("returnidetags.cs")
                .Single(x => x.Code == "IDE0005")
                .Tags);
        }

        [Fact]
        // issue: https://github.com/OmniSharp/omnisharp-roslyn/issues/1619
        public async Task DoesNotErroneouslyReportCS0019_WhenComparingToDefault()
        {
            await ReadyHost(true);
            AddFilesToWorkspace(
                new TestFile("a.cs", "class C1 { bool Test(object input) => input == default; }"));

            await WaitForDiagnostics();

            var allDiagnostics = GetDiagnostics("a.cs").Where(x => x.Code == "CS0019");
            Assert.Empty(allDiagnostics);
        }
    }
}
