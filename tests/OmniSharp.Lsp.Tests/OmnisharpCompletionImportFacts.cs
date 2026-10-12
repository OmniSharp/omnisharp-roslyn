using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using TestUtility;
using Xunit;

namespace OmniSharp.Lsp.Tests
{
    public class OmnisharpCompletionImportFacts : OmnisharpCompletionTestBase
    {
        public OmnisharpCompletionImportFacts(ITestOutputHelper output)
            : base(output, new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["RoslynExtensionsOptions:EnableImportCompletion"] = "true"
                })
                .Build())
        {
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ImportCompletionResolvesOnSubsequentQueries(string filename)
        {
            const string input =
@"public class Class1 {
    public Class1()
    {
        Gui$$
    }
}";


            // First completion request should kick off the task to update the completion cache.
            var completions = await FindCompletionsAsync(filename, input);
            if (!completions.IsIncomplete)
            {
                Assert.Contains("Guid", completions.Items.Select(c => c.TextEdit.TextEdit.NewText));
                return; // No need to wait for the cache to be populated.
            }

            Assert.DoesNotContain("Guid", completions.Items.Select(c => c.TextEdit.TextEdit.NewText));

            // Populating the completion cache should take no more than a few ms, don't let it take too
            // long
            CancellationTokenSource cts = new CancellationTokenSource(millisecondsDelay: ImportCompletionTimeout);
            await Task.Run(async () =>
            {
                while (completions.IsIncomplete)
                {
                    completions = await FindCompletionsAsync(filename, input);
                    cts.Token.ThrowIfCancellationRequested();
                }
            }, cts.Token);

            Assert.False(completions.IsIncomplete);
            Assert.Contains("Guid", completions.Items.Select(c => c.TextEdit.TextEdit.NewText));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ImportCompletion_LocalsPrioritizedOverImports(string filename)
        {

            const string input =
@"public class Class1 {
    public Class1()
    {
        string guid;
        Gui$$
    }
}";

            var completions = await FindCompletionsWithImportedAsync(filename, input);
            CompletionItem localCompletion = completions.Items.First(c => c.TextEdit.TextEdit.NewText == "guid");
            CompletionItem typeCompletion = completions.Items.First(c => c.TextEdit.TextEdit.NewText == "Guid");
            Assert.True(localCompletion.Data.ToObject<(long, int)>().Item2 < typeCompletion.Data.ToObject<(long, int)>().Item2);
            Assert.StartsWith("0", localCompletion.SortText);
            Assert.StartsWith("1", typeCompletion.SortText);
            VerifySortOrders(completions.Items);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ImportCompletions_IncludesExtensionMethods(string filename)
        {
            const string input =
@"namespace N1
{
    public class C1
    {
        public void M(object o)
        {
            o.$$
        }
    }
}
namespace N2
{
    public static class ObjectExtensions
    {
        public static void Test(this object o)
        {
        }
    }
}";

            var completions = await FindCompletionsWithImportedAsync(filename, input);
            Assert.Contains("Test", completions.Items.Select(c => c.TextEdit.TextEdit.NewText));
            VerifySortOrders(completions.Items);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ImportCompletion_ResolveAddsImportEdit(string filename)
        {
            const string input =
@"namespace N1
{
    public class C1
    {
        public void M(object o)
        {
            o.$$
        }
    }
}
namespace N2
{
    public static class ObjectExtensions
    {
        public static void Test(this object o)
        {
        }
    }
}";

            var completions = await FindCompletionsWithImportedAsync(filename, input);
            var resolved = await ResolveCompletionAsync(completions.Items.First(c => c.TextEdit.TextEdit.NewText == "Test"));

            Assert.Single(resolved.AdditionalTextEdits);
            var additionalEdit = resolved.AdditionalTextEdits.First();
            Assert.Equal(NormalizeNewlines("using N2;\n\n"), additionalEdit.NewText);
            Assert.Equal(0, additionalEdit.Range.Start.Line);
            Assert.Equal(0, additionalEdit.Range.Start.Character);
            Assert.Equal(0, additionalEdit.Range.End.Line);
            Assert.Equal(0, additionalEdit.Range.End.Character);
            VerifySortOrders(completions.Items);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ImportCompletion_OnLine0(string filename)
        {
            const string input = @"$$";

            var completions = await FindCompletionsWithImportedAsync(filename, input);
            var resolved = await ResolveCompletionAsync(completions.Items.First(c => c.TextEdit.TextEdit.NewText == "Console"));

            Assert.Single(resolved.AdditionalTextEdits);
            var additionalEdit = resolved.AdditionalTextEdits.First();
            Assert.Equal(NormalizeNewlines("using System;\n\n"),
                         NormalizeNewlines(additionalEdit.NewText));
            Assert.Equal(0, additionalEdit.Range.Start.Line);
            Assert.Equal(0, additionalEdit.Range.Start.Character);
            Assert.Equal(0, additionalEdit.Range.End.Line);
            Assert.Equal(0, additionalEdit.Range.End.Character);
            VerifySortOrders(completions.Items);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task SelectsLastInstanceOfCompletion(string filename)
        {
            const string input =
@"namespace N1
{
    public class C1
    {
        public void M(object o)
        {
            /*Guid*/$$//Guid
        }
    }
}
namespace N2
{
    public static class ObjectExtensions
    {
        public static void Test(this object o)
        {
        }
    }
}";

            var completions = await FindCompletionsWithImportedAsync(filename, input);
            var resolved = await ResolveCompletionAsync(completions.Items.First(c => c.TextEdit.TextEdit.NewText == "Guid"));

            Assert.Single(resolved.AdditionalTextEdits);
            var additionalEdit = resolved.AdditionalTextEdits.First();
            Assert.Equal(NormalizeNewlines("using System;\n\n"),
                         additionalEdit.NewText);
            Assert.Equal(0, additionalEdit.Range.Start.Line);
            Assert.Equal(0, additionalEdit.Range.Start.Character);
            Assert.Equal(0, additionalEdit.Range.End.Line);
            Assert.Equal(0, additionalEdit.Range.End.Character);
            VerifySortOrders(completions.Items);
        }

        [Theory(Skip = "Skipping for being flaky")]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task UsingsAddedInOrder(string filename)
        {

            const string input =
@"using N1;
using N3;
namespace N1
{
    public class C1
    {
        public void M(object o)
        {
            $$
        }
    }
}
namespace N2
{
    public class C2
    {
    }
}
namespace N3
{
    public class C3
    {
    }
}";

            var completions = await FindCompletionsWithImportedAsync(filename, input);
            var resolved = await ResolveCompletionAsync(completions.Items.First(c => c.TextEdit.TextEdit.NewText == "C2"));

            Assert.Single(resolved.AdditionalTextEdits);
            var additionalEdit = resolved.AdditionalTextEdits.First();
            Assert.Equal(NormalizeNewlines("using N2;\n"),
                         additionalEdit.NewText);
            Assert.Equal(1, additionalEdit.Range.Start.Line);
            Assert.Equal(0, additionalEdit.Range.Start.Character);
            Assert.Equal(1, additionalEdit.Range.End.Line);
            Assert.Equal(0, additionalEdit.Range.End.Character);
            VerifySortOrders(completions.Items);
        }
    }
}
