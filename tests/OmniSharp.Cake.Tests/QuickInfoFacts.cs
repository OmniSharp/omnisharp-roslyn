using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Services.RequestHandlers;
using OmniSharp.Cake.Services.RequestHandlers.Completion;
using OmniSharp.Models;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.v1.Completion;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class QuickInfoFacts : CakeSingleRequestHandlerTestFixture<QuickInfoHandler>
    {
        private readonly ILogger _logger;

        public QuickInfoFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
            _logger = LoggerFactory.CreateLogger<QuickInfoFacts>();
        }

        protected override string EndpointName => OmniSharpEndpoints.QuickInfo;

        [Fact]
        public async Task ShouldGetQuickInfo()
        {
            const string input = "Informa$$tion(\"Hello\");";

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");
                var quickInfo = await GetQuickInfo(fileName, input, host);

                Assert.StartsWith("```csharp\nvoid Information(string value)", quickInfo.Markdown);
            }
        }

        private async Task<QuickInfoResponse> GetQuickInfo(string filename, string source, OmniSharpTestHost host, char? triggerChar = null, TestFile[] additionalFiles = null)
        {
            var testFile = new TestFile(filename, source);

            var files = new[] { testFile };
            if (additionalFiles is object)
            {
                files = files.Concat(additionalFiles).ToArray();
            }

            host.AddFilesToWorkspace(files);
            var point = testFile.Content.GetPointFromPosition();

            var request = new QuickInfoRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = testFile.FileName,
                Buffer = testFile.Content.Code
            };

            var updateBufferRequest = new UpdateBufferRequest
            {
                Buffer = request.Buffer,
                Column = request.Column,
                FileName = request.FileName,
                Line = request.Line,
                FromDisk = false
            };

            await GetUpdateBufferHandler(host).Handle(updateBufferRequest);

            var requestHandler = GetRequestHandler(host);

            return await requestHandler.Handle(request);
        }
    }
}
