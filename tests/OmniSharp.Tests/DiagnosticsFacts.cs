using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class DiagnosticsFacts
    {
        [Fact]
        public async Task CodeCheckSpecifiedFileOnly()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", "class C { int n = true; }" }
            });

            var controller = new OmnisharpController(workspace, new FakeOmniSharpOptions());
            var quickFixes = await controller.CodeCheck(new Request() { FileName = "a.cs" });

            Assert.Equal(1, quickFixes.QuickFixes.Count());
            Assert.Equal("a.cs", quickFixes.QuickFixes.First().FileName);
        }

        [Fact]
        public async Task CheckAllFiles()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", "class C1 { int n = true; }" },
                { "b.cs", "class C2 { int n = true; }" },
            });

            var controller = new OmnisharpController(workspace, new FakeOmniSharpOptions());
            var quickFixes = await controller.CodeCheck(new Request());

            Assert.Equal(2, quickFixes.QuickFixes.Count());
        }
    }
}