using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GoToFileFacts
    {
        [Fact]
        public async Task ReturnsAListOfAllWorkspaceFiles()
        {
            var source1 = @"class Foo {}";
            var source2 = @"class Bar {}";

            var workspace = await TestHelpers.CreateWorkspace(new []
            {
                new TestFile("foo.cs", source1),
                new TestFile("bar.cs", source2)
            });

            var controller = new GotoFileService(workspace);
            var response = await controller.Handle(new GotoFileRequest());

            Assert.Equal(2, response.QuickFixes.Count());
            Assert.Equal("foo.cs", response.QuickFixes.ElementAt(0).FileName);
            Assert.Equal("bar.cs", response.QuickFixes.ElementAt(1).FileName);
        }

        [Fact]
        public async Task ReturnsEmptyResponseForEmptyWorskpace()
        {
            var workspace = await TestHelpers.CreateWorkspace();
            var controller = new GotoFileService(workspace);
            var response = await controller.Handle(new GotoFileRequest());

            Assert.Equal(0, response.QuickFixes.Count());
        }
    }
}
