using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Services.RequestHandlers.Completion;
using OmniSharp.Cake.Services.RequestHandlers.Intellisense;
using OmniSharp.Models.AutoComplete;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.v1.Completion;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class CompletionFacts : CakeSingleRequestHandlerTestFixture<CompletionHandler>
    {
        private readonly ILogger _logger;

        public CompletionFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
            _logger = LoggerFactory.CreateLogger<AutoCompleteFacts>();
        }

        protected override string EndpointName => OmniSharpEndpoints.Completion;

        [Fact]
        public async Task ShouldGetCompletionFromHostObject()
        {
            const string input = @"TaskSe$$";

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");
                var completions = await FindCompletionsAsync(fileName, input, host);

                Assert.Contains("TaskSetup", completions.Items.Select(c => c.Label));
                Assert.Contains("TaskSetup", completions.Items.Select(c => c.InsertText));
            }
        }

        [Fact]
        public async Task ShouldGetCompletionFromDSL()
        {
            const string input =
                @"Task(""Test"")
                    .Does(() => {
                        Inform$$
                    });";

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");
                var completions = await FindCompletionsAsync(fileName, input, host);

                Assert.Contains("Information", completions.Items.Select(c => c.Label));
                Assert.Contains("Information", completions.Items.Select(c => c.InsertText));
            }
        }

        [Fact]
        public async Task ShouldResolveFromDSL()
        {
            const string input =
                @"Task(""Test"")
                    .Does(() => {
                        Inform$$
                    });";

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");
                var completion = (await FindCompletionsAsync(fileName, input, host))
                    .Items.First(x => x.Preselect && x.InsertText == "Information");

                var resolved = await ResolveCompletionAsync(completion, host);

                Assert.StartsWith(
                    "```csharp\nvoid Information(string format, params object[] args)",
                    resolved.Item?.Documentation);
            }
        }

        // [Fact]
        // public async Task ShouldGetCompletionWithAdditionalTextEdits()
        // {
        //     const string input = @"Regex.Repl$$";
        //
        //     using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
        //     using (var host = CreateOmniSharpHost(testProject.Directory))
        //     {
        //         var fileName = Path.Combine(testProject.Directory, "build.cake");
        //         var completions = await FindCompletionsAsync(fileName, input, host);
        //
        //         Assert.Contains("Replace", completions.Items.Select(c => c.Label));
        //         Assert.Contains("Replace", completions.Items.Select(c => c.InsertText));
        //     }
        // }

        private async Task<CompletionResponse> FindCompletionsAsync(string filename, string source, OmniSharpTestHost host, char? triggerChar = null, TestFile[] additionalFiles = null)
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

        private static async Task<CompletionResolveResponse> ResolveCompletionAsync(CompletionItem completionItem, OmniSharpTestHost testHost)
            => await GetResolveHandler(testHost).Handle(new CompletionResolveRequest { Item = completionItem });

        private static CompletionResolveHandler GetResolveHandler(OmniSharpTestHost host)
            => host.GetRequestHandler<CompletionResolveHandler>(OmniSharpEndpoints.CompletionResolve, Constants.LanguageNames.Cake);
    }
}
