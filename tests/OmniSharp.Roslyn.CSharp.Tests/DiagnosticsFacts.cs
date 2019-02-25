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
    public class DiagnosticsFacts : AbstractSingleRequestHandlerTestFixture<CodeCheckService>
    {
        public DiagnosticsFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.CodeCheck;

        [Fact]
        public async Task CodeCheckSpecifiedFileOnly()
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(new TestFile("a.cs", "class C { int n = true; }"));
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var quickFixes = await requestHandler.Handle(new CodeCheckRequest() { FileName = "a.cs" });

            Assert.Single(quickFixes.QuickFixes);
            Assert.Equal("a.cs", quickFixes.QuickFixes.First().FileName);
        }

        [Fact]
        public async Task CheckAllFiles()
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(
                new TestFile("a.cs", "class C1 { int n = true; }"),
                new TestFile("b.cs", "class C2 { int n = true; }"));

            var handler = GetRequestHandler(SharedOmniSharpTestHost);
            var quickFixes = await handler.Handle(new CodeCheckRequest());

            Assert.Equal(2, quickFixes.QuickFixes.Count());
        }

        [Fact]
        public async Task WhenUnusedImportExistsWithoutAnalyzersEnabled_ThenReturnEmptyTags()
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(
                new TestFile("returnemptytags.cs", @"
                    using System.IO;
                "));

            var handler = GetRequestHandler(SharedOmniSharpTestHost);
            var quickFixes = await handler.Handle(new CodeCheckRequest()
            {
                FileName = "returnemptytags.cs"
            });

            Assert.Empty(quickFixes.QuickFixes.OfType<DiagnosticLocation>().Single().Tags);
        }

        [Fact(Skip="Placeholder, requires analyzers to be implemented.")]
        public async Task WhenUnusedImportIsFoundAndAnalyzersEnabled_ThenReturnUnnecessaryTag()
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(
                new TestFile("returnidetags.cs", @"
                    using System.IO;
                "));

            // TODO: Enable analyzers for this test, should return IDE analyzer with Unnesessary tag.
            var handler = GetRequestHandler(SharedOmniSharpTestHost);
            var quickFixes = await handler.Handle(new CodeCheckRequest()
            {
                FileName = "returnidetags.cs"
            });

            Assert.Contains("Unnecessary", quickFixes
                .QuickFixes
                .OfType<DiagnosticLocation>()
                .Single(x => x.Id == "IDE0xxx")
                .Tags);
        }
    }
}
