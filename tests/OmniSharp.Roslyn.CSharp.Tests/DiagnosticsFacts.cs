using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class DiagnosticsFacts
    {
        [Fact]
        public async Task CodeCheckSpecifiedFileOnly()
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", "class C { int n = true; }" }
            });

            var controller = new CodeCheckService(workspace);
            var quickFixes = await controller.Handle(new CodeCheckRequest() { FileName = "a.cs" });

            Assert.Equal(1, quickFixes.QuickFixes.Count());
            Assert.Equal("a.cs", quickFixes.QuickFixes.First().FileName);
        }

        [Fact]
        public async Task CheckAllFiles()
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "a.cs", "class C1 { int n = true; }" },
                { "b.cs", "class C2 { int n = true; }" },
            });

            var controller = new CodeCheckService(workspace);
            var quickFixes = await controller.Handle(new CodeCheckRequest());

            Assert.Equal(2, quickFixes.QuickFixes.Count());
        }
    }
}
