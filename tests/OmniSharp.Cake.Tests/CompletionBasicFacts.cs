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
    public class CompletionBasicFacts : CompletionTestBase
    {
        public CompletionBasicFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        [Fact]
        public async Task ShouldGetCompletionFromHostObject()
        {
            const string input = @"TaskSe$$";

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
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

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
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

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
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
    }
}
