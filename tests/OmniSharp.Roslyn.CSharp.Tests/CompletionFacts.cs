using System.Threading.Tasks;
using OmniSharp.Models.V2.Completion;
using OmniSharp.Roslyn.CSharp.Services.IntelliSense.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CompletionFacts : AbstractSingleRequestHandlerTestFixture<CompletionService>
    {
        public CompletionFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.Completion;

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async void Empty_file_contains_keywords(string fileName)
        {
            const string markup = @"$$";

            var (items, isSuggestionMode) = await RequestCompletionAsync(fileName, markup);

            Assert.False(isSuggestionMode);
            Assert.Contains(items, item =>
            {
                return item.DisplayText == "class"
                    && item.Kind == "Keyword";
            });
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async void Attributes_do_not_have_attribute_suffix(string fileName)
        {
            const string markup = @"
class SuperAttribute : System.Attribute { }

[S$$]
class C
{
}
";

            var (items, isSuggestionMode) = await RequestCompletionAsync(fileName, markup);

            Assert.False(isSuggestionMode);
            Assert.Contains(items, item => item.DisplayText == "Super");
            Assert.DoesNotContain(items, item => item.DisplayText == "SuperAttribute");
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async void Description_has_minimally_qualified_crefs(string fileName)
        {
            const string markup = @"
using System.Text;

/// <summary>My Crefs are <see cref=""System.Text.StringBuilder""/> and <see cref=""System.IO.Path""/>.</summary>
class B { }

class C
{
    void M()
    {
        new B$$
    }
}
";


            await AssertItemDescriptionAsync(fileName, markup,
                displayText: "B", expectedDescription:
                "class B\r\nMy Crefs are StringBuilder and System.IO.Path.");
        }

        private async Task AssertItemDescriptionAsync(string fileName, string markup, string displayText, string expectedDescription)
        {
            var testFile = new TestFile(fileName, markup);

            using (var host = CreateOmniSharpHost(testFile))
            {
                var (items, _) = await RequestCompletionAsync(testFile, host);

                var item = await ResolveCompletionItemAsync(displayText, items, testFile, host);

                Assert.Equal(expectedDescription, item.Description);
            }
        }

        private Task<(CompletionItem[] items, bool isSuggestionMode)> RequestCompletionAsync(string fileName, string markup)
        {
            var testFile = new TestFile(fileName, markup);

            using (var host = CreateOmniSharpHost(testFile))
            {
                return RequestCompletionAsync(testFile, host);
            }
        }

        private async Task<(CompletionItem[] items, bool isSuggestionMode)> RequestCompletionAsync(TestFile testFile, OmniSharpTestHost host)
        {
            var handler = GetRequestHandler(host);

            var request = new CompletionRequest
            {
                FileName = testFile.FileName,
                Position = testFile.Content.Position,
                Trigger = new CompletionTrigger
                {
                    Kind = CompletionTriggerKind.Invoke
                }
            };

            var response = await handler.Handle(request);

            return (response.Items, response.IsSuggestionMode);
        }

        private async Task<CompletionItem> ResolveCompletionItemAsync(string displayText, CompletionItem[] items, TestFile testFile, OmniSharpTestHost host)
        {
            var itemIndex = GetItemIndex(displayText, items);
            Assert.True(itemIndex >= 0);

            var handler = GetRequestHandler(host);

            var request = new CompletionItemResolveRequest
            {
                DisplayText = displayText,
                FileName = testFile.FileName,
                ItemIndex = itemIndex
            };

            var response = await handler.Handle(request);

            return response.Item;
        }

        private int GetItemIndex(string displayText, CompletionItem[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].DisplayText == displayText)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
