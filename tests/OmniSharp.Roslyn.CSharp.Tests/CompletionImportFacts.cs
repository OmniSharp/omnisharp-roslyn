using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Roslyn.CSharp.Services.Completion;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public abstract class CompletionImportFactsBase : CompletionTestBase
    {
        protected CompletionImportFactsBase(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected abstract bool UseAsyncCompletion { get; }

        protected override OmniSharpTestHost CreateSharedOmniSharpTestHost()
        {
            var configurationData = new List<KeyValuePair<string, string>>
            {
                new("RoslynExtensionsOptions:EnableImportCompletion", "true")
            };
            if (UseAsyncCompletion)
            {
                configurationData.Add(new("RoslynExtensionsOptions:EnableAsyncCompletion", "true"));
            }

            return CreateOmniSharpHost(configurationData: configurationData);
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

            var host = SharedOmniSharpTestHost;

            // First completion request should kick off the task to update the completion cache.
            var completions = await FindCompletionsAsync(filename, input, host);
            if (!completions.IsIncomplete)
            {
                Assert.Contains("Guid", completions.Items.Select(c => c.TextEdit.NewText));
                return; // No need to wait for the cache to be populated.
            }

            Assert.DoesNotContain("Guid", completions.Items.Select(c => c.TextEdit.NewText));
            Assert.All(completions.Items, c => Assert.False(c.HasAfterInsertStep));

            // Populating the completion cache should take no more than a few ms, don't let it take too
            // long
            CancellationTokenSource cts = new CancellationTokenSource(millisecondsDelay: ImportCompletionTimeout);
            await Task.Run(async () =>
            {
                while (completions.IsIncomplete)
                {
                    completions = await FindCompletionsAsync(filename, input, host);
                    cts.Token.ThrowIfCancellationRequested();
                }
            }, cts.Token);

            Assert.False(completions.IsIncomplete);
            Assert.Contains("Guid", completions.Items.Select(c => c.TextEdit.NewText));
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

            var host = SharedOmniSharpTestHost;
            var completions = await FindCompletionsWithImportedAsync(filename, input, host);
            CompletionItem localCompletion = completions.Items.First(c => c.TextEdit.NewText == "guid");
            CompletionItem typeCompletion = completions.Items.First(c => c.TextEdit.NewText == "Guid");
            Assert.True(localCompletion.Data.Index < typeCompletion.Data.Index);
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

            var host = SharedOmniSharpTestHost;
            var completions = await FindCompletionsWithImportedAsync(filename, input, host);
            Assert.Contains("Test", completions.Items.Select(c => c.TextEdit.NewText));
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

            var host = SharedOmniSharpTestHost;
            var completions = await FindCompletionsWithImportedAsync(filename, input, host);
            var resolved = await ResolveCompletionAsync(completions.Items.First(c => c.TextEdit.NewText == "Test"), host);

            Assert.Single(resolved.Item.AdditionalTextEdits);
            var additionalEdit = resolved.Item.AdditionalTextEdits[0];
            Assert.Equal(NormalizeNewlines("using N2;\n\n"),
                         additionalEdit.NewText);
            Assert.Equal(0, additionalEdit.StartLine);
            Assert.Equal(0, additionalEdit.StartColumn);
            Assert.Equal(0, additionalEdit.EndLine);
            Assert.Equal(0, additionalEdit.EndColumn);
            VerifySortOrders(completions.Items);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ImportCompletion_OnLine0(string filename)
        {
            const string input = @"$$";

            var host = SharedOmniSharpTestHost;
            var completions = await FindCompletionsWithImportedAsync(filename, input, host);
            var resolved = await ResolveCompletionAsync(completions.Items.First(c => c.TextEdit.NewText == "Console"), host);

            Assert.Single(resolved.Item.AdditionalTextEdits);
            var additionalEdit = resolved.Item.AdditionalTextEdits[0];
            Assert.Equal(NormalizeNewlines("using System;\n\n"),
                         NormalizeNewlines(additionalEdit.NewText));
            Assert.Equal(0, additionalEdit.StartLine);
            Assert.Equal(0, additionalEdit.StartColumn);
            Assert.Equal(0, additionalEdit.EndLine);
            Assert.Equal(0, additionalEdit.EndColumn);
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

            var host = SharedOmniSharpTestHost;
            var completions = await FindCompletionsWithImportedAsync(filename, input, host);
            var resolved = await ResolveCompletionAsync(completions.Items.First(c => c.TextEdit.NewText == "Guid"), host);

            Assert.Single(resolved.Item.AdditionalTextEdits);
            var additionalEdit = resolved.Item.AdditionalTextEdits[0];
            Assert.Equal(NormalizeNewlines("using System;\n\n"),
                         additionalEdit.NewText);
            Assert.Equal(0, additionalEdit.StartLine);
            Assert.Equal(0, additionalEdit.StartColumn);
            Assert.Equal(0, additionalEdit.EndLine);
            Assert.Equal(0, additionalEdit.EndColumn);
            VerifySortOrders(completions.Items);
        }

        [Theory]
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

            var host = SharedOmniSharpTestHost;
            var completions = await FindCompletionsWithImportedAsync(filename, input, host);
            var resolved = await ResolveCompletionAsync(completions.Items.First(c => c.TextEdit.NewText == "C2"), host);

            Assert.Single(resolved.Item.AdditionalTextEdits);
            var additionalEdit = resolved.Item.AdditionalTextEdits[0];
            Assert.Equal(NormalizeNewlines("using N2;\n"),
                         additionalEdit.NewText);
            Assert.Equal(1, additionalEdit.StartLine);
            Assert.Equal(0, additionalEdit.StartColumn);
            Assert.Equal(1, additionalEdit.EndLine);
            Assert.Equal(0, additionalEdit.EndColumn);
            VerifySortOrders(completions.Items);
        }
    }

    public sealed class CompletionImportSyncFacts : CompletionImportFactsBase
    {
        public CompletionImportSyncFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override bool UseAsyncCompletion => false;
    }

    public sealed class CompletionImportAsyncFacts : CompletionImportFactsBase
    {
        public CompletionImportAsyncFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override bool UseAsyncCompletion => true;
    }
}
