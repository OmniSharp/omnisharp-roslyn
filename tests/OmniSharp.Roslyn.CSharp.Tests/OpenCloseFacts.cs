using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Files;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class OpenCloseFacts : AbstractTestFixture
    {
        public OpenCloseFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task AddsOpenFile()
        {
            var source1 = @"using System; class Foo { }";
            var source2 = @"class Bar { private Foo foo; }";

            var workspace = await CreateWorkspaceAsync(
                new TestFile("foo.cs", source1),
                new TestFile("bar.cs", source2));

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

            var workspace = await CreateWorkspaceAsync(
                new TestFile("foo.cs", source1),
                new TestFile("bar.cs", source2));

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
