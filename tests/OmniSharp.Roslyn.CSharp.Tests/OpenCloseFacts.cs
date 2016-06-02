using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Files;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class OpenCloseFacts
    {
        [Fact]
        public async Task AddsOpenFile()
        {
            var source1 = @"using System; class Foo { }";
            var source2 = @"class Bar { private Foo foo; }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var document = workspace.GetDocumentId("foo.cs");

            var controller = new FileOpenService(workspace);
            var response = await controller.Handle(new FileOpenRequest
            {
                FileName = "foo.cs"
            });

            Assert.True(workspace.IsDocumentOpen(document));
        }

        public async Task RemovesOpenFile()
        {
            var source1 = @"using System; class Foo { }";
            var source2 = @"class Bar { private Foo foo; }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var document = workspace.GetDocumentId("foo.cs");

            var openController = new FileOpenService(workspace);
            var closeController = new FileOpenService(workspace);
            var openResponse = await openController.Handle(new FileOpenRequest
            {
                FileName = "foo.cs"
            });

            Assert.True(workspace.IsDocumentOpen(document));

            var closeResponse = await openController.Handle(new FileOpenRequest
            {
                FileName = "foo.cs"
            });

            Assert.False(workspace.IsDocumentOpen(document));
        }
    }
}
