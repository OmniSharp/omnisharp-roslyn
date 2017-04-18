using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.FileOpen;
using OmniSharp.Roslyn.CSharp.Services.Files;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class OpenCloseFacts : AbstractSingleRequestHandlerTestFixture<FileOpenService>
    {
        public OpenCloseFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.Open;

        [Fact]
        public async Task AddsOpenFile()
        {
            var testFile1 = new TestFile("foo.cs", @"using System; class Foo { }");
            var testFile2 = new TestFile("bar.cs", @"class Bar { private Foo foo; }");

            using (var host = CreateOmniSharpHost(testFile1, testFile2))
            {
                var documentId = host.Workspace.GetDocumentId("foo.cs");
                var requestHandler = GetRequestHandler(host);

                var request = new FileOpenRequest
                {
                    FileName = "foo.cs"
                };

                var response = await requestHandler.Handle(request);

                Assert.True(host.Workspace.IsDocumentOpen(documentId));
            }
        }
    }
}
