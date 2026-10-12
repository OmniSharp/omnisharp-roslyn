using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Services.RequestHandlers.Completion;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.v1.Completion;
using TestUtility;
using Xunit;

namespace OmniSharp.Cake.Tests
{
    public abstract class CompletionTestBase : CakeSingleRequestHandlerTestFixture<CompletionHandler>
    {
        protected const int ImportCompletionTimeout = 2000;

        protected CompletionTestBase(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.Completion;

        protected async Task<CompletionResponse> FindCompletionsAsync(string filename, string source, OmniSharpTestHost host, char? triggerChar = null, TestFile[] additionalFiles = null)
        {
            var testFile = new TestFile(filename, source);

            var files = new[] { testFile };
            if (additionalFiles is object)
            {
                files = files.Concat(additionalFiles).ToArray();
            }

            host.AddFilesToWorkspace(files);
            var point = testFile.Content.GetPointFromPosition();

            var request = new CompletionRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = testFile.FileName,
                Buffer = testFile.Content.Code,
                CompletionTrigger = triggerChar is object ? CompletionTriggerKind.TriggerCharacter : CompletionTriggerKind.Invoked,
                TriggerCharacter = triggerChar
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

        protected static async Task<CompletionResolveResponse> ResolveCompletionAsync(CompletionItem completionItem, OmniSharpTestHost testHost)
            => await GetResolveHandler(testHost).Handle(new CompletionResolveRequest { Item = completionItem });

        private static CompletionResolveHandler GetResolveHandler(OmniSharpTestHost host)
            => host.GetRequestHandler<CompletionResolveHandler>(OmniSharpEndpoints.CompletionResolve, Constants.LanguageNames.Cake);

    }
}
