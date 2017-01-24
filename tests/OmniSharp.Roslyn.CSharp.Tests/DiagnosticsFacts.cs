using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class DiagnosticsFacts : AbstractTestFixture
    {
        public DiagnosticsFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task CodeCheckSpecifiedFileOnly()
        {
            var workspace = await CreateWorkspaceAsync(new TestFile("a.cs", "class C { int n = true; }"));

            var controller = new CodeCheckService(workspace);
            var quickFixes = await controller.Handle(new CodeCheckRequest() { FileName = "a.cs" });

            Assert.Equal(1, quickFixes.QuickFixes.Count());
            Assert.Equal("a.cs", quickFixes.QuickFixes.First().FileName);
        }

        [Fact]
        public async Task CheckAllFiles()
        {
            var workspace = await CreateWorkspaceAsync(
                new TestFile("a.cs", "class C1 { int n = true; }"),
                new TestFile("b.cs", "class C2 { int n = true; }"));

            var controller = new CodeCheckService(workspace);
            var quickFixes = await controller.Handle(new CodeCheckRequest());

            Assert.Equal(2, quickFixes.QuickFixes.Count());
        }
    }
}
