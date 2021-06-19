using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Services.RequestHandlers.Completion;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.v1.Completion;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class CompletionFacts : CakeSingleRequestHandlerTestFixture<CompletionHandler>
    {
        private const int ImportCompletionTimeout = 1000;
        private readonly ILogger _logger;

        public CompletionFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
            _logger = LoggerFactory.CreateLogger<CompletionFacts>();
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
                Assert.Contains("TaskSetup", completions.Items.Select(c => c.TextEdit.NewText));
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
                Assert.Contains("Information", completions.Items.Select(c => c.TextEdit.NewText));
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
                    .Items.First(x => x.TextEdit.NewText == "Information");

                var resolved = await ResolveCompletionAsync(completion, host);

                Assert.StartsWith(
                    "```csharp\nvoid Information(string format, params object[] args)",
                    resolved.Item?.Documentation);
            }
        }

        [Fact]
        public async Task ShouldRemoveAdditionalTextEditsFromResolvedCompletions()
        {
            const string input = @"var regex = new Rege$$";

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory,
                new[] { new KeyValuePair<string, string>("RoslynExtensionsOptions:EnableImportCompletion", "true") }))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");

                // First completion request should kick off the task to update the completion cache.
                var completions = await FindCompletionsAsync(fileName, input, host);
                Assert.True(completions.IsIncomplete);
                Assert.DoesNotContain("Regex", completions.Items.Select(c => c.TextEdit.NewText));

                // Populating the completion cache should take no more than a few ms, don't let it take too
                // long
                var cts = new CancellationTokenSource(millisecondsDelay: ImportCompletionTimeout);
                await Task.Run(async () =>
                {
                    while (completions.IsIncomplete)
                    {
                        completions = await FindCompletionsAsync(fileName, input, host);
                        cts.Token.ThrowIfCancellationRequested();
                    }
                }, cts.Token);

                Assert.False(completions.IsIncomplete);
                Assert.Contains("Regex", completions.Items.Select(c => c.TextEdit.NewText));

                var completion = completions.Items.First(c => c.TextEdit.NewText == "Regex");
                var resolved = await ResolveCompletionAsync(completion, host);

                // Due to the fact that AdditionalTextEdits return the complete buffer, we can't currently use that in Cake.
                // Revisit when we have a solution. At this point it's probably just best to remove AdditionalTextEdits.
                Assert.Null(resolved.Item.AdditionalTextEdits);
            }
        }

        [Fact]
        public async Task ShouldGetAdditionalTextEditsFromOverrideCompletion()
        {
            const string source = @"
class Foo
{
    public virtual void Test(string text) {}
    public virtual void Test(string text, string moreText) {}
}

class FooChild : Foo
{
    override $$
}
";

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");
                var completions = await FindCompletionsAsync(fileName, source, host);
                Assert.Equal(
                    new[]
                    {
                        "Equals(object obj)", "GetHashCode()", "Test(string text)",
                        "Test(string text, string moreText)", "ToString()"
                    },
                    completions.Items.Select(c => c.Label));
                Assert.Equal(new[]
                    {
                        "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                        "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                        "public override void Test(string text)\n    {\n        base.Test(text);$0\n    \\}",
                        "public override void Test(string text, string moreText)\n    {\n        base.Test(text, moreText);$0\n    \\}",
                        "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                    },
                    completions.Items.Select(c => c.TextEdit.NewText));

                Assert.Equal(new[]
                    {
                        "override Equals",
                        "override GetHashCode",
                        "override Test",
                        "override Test",
                        "override ToString"
                    },
                    completions.Items.Select(c => c.FilterText));

                Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));

                Assert.All(completions.Items.Select(c => c.TextEdit),
                    r =>
                    {
                        Assert.Equal(9, r.StartLine);
                        Assert.Equal(4, r.StartColumn);
                        Assert.Equal(9, r.EndLine);
                        Assert.Equal(13, r.EndColumn);
                    });

                Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
            }
        }

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
