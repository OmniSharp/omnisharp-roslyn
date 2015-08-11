using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class GoToFileFacts
    {
        [Fact]
        public void ReturnsAListOfAllWorkspaceFiles()
        {
            var source1 = @"class Foo {}";
            var source2 = @"class Bar {}";

            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var controller = new OmnisharpController(workspace, new FakeOmniSharpOptions());
            var response = controller.GoToFile(new Request());

            Assert.Equal(2, response.QuickFixes.Count());
            Assert.Equal("foo.cs", response.QuickFixes.ElementAt(0).FileName);
            Assert.Equal("bar.cs", response.QuickFixes.ElementAt(1).FileName);
        }

        [Fact]
        public void ReturnsEmptyResponseForEmptyWorskpace()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>());
            var controller = new OmnisharpController(workspace, new FakeOmniSharpOptions());
            var response = controller.GoToFile(new Request());

            Assert.Equal(0, response.QuickFixes.Count());
        }
    }
}
