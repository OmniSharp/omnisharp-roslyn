using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Services.RequestHandlers.Files;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.FileOpen;
using OmniSharp.Models.UpdateBuffer;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace OmniSharp.Cake.Tests
{
    public class FileOpenFacts : CakeSingleRequestHandlerTestFixture<FileOpenHandler>
    {
        public FileOpenFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.Open;

        [Fact]
        public async Task AddsOpenFile()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var filename = Path.Combine(testProject.Directory, "build.cake");
                var documentId = host.Workspace.GetDocumentId(filename);

                Assert.False(host.Workspace.IsDocumentOpen(documentId));

                var request = new FileOpenRequest
                {
                    FileName = filename
                };

                var requestHandler = GetRequestHandler(host);

                _ = await requestHandler.Handle(request);

                Assert.True(host.Workspace.IsDocumentOpen(documentId));
            }
        }
    }
}
