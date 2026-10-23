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
    public class CompletionImportFacts : CompletionTestBase
    {
        public CompletionImportFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        [Fact]
        public async Task ShouldRemoveAdditionalTextEditsFromResolvedCompletions()
        {
            const string input = @"var regex = new Rege$$";

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
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
                await Task.Delay(ImportCompletionTimeout);

                completions = await FindCompletionsAsync(fileName, input, host);
                Assert.False(completions.IsIncomplete);
                Assert.Contains("Regex", completions.Items.Select(c => c.TextEdit.NewText));

                var completion = completions.Items.First(c => c.TextEdit.NewText == "Regex");
                var resolved = await ResolveCompletionAsync(completion, host);

                // Due to the fact that AdditionalTextEdits return the complete buffer, we can't currently use that in Cake.
                // Revisit when we have a solution. At this point it's probably just best to remove AdditionalTextEdits.
                Assert.Null(resolved.Item.AdditionalTextEdits);
            }
        }
    }
}
