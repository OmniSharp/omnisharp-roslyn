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

            var (items, _) = await RequestCompletionAsync(fileName, markup);

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
}";

            var (items, _) = await RequestCompletionAsync(fileName, markup);

            Assert.Contains(items, item => item.DisplayText == "Super");
            Assert.DoesNotContain(items, item => item.DisplayText == "SuperAttribute");
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Returns_members_in_object_initializer_context(string fileName)
        {
            const string markup = @"
class B
{
    public string Foo { get; set; }
}

class C
{
    void M()
    {
        var c = new B
        {
            $$
        }
    }
}";

            var (items, _) = await RequestCompletionAsync(fileName, markup);
            Assert.Contains(items, item => item.DisplayText == "Foo");
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Returns_named_parameter_inside_a_method_call(string fileName)
        {
            const string markup = @"
class Greeter
{
    public void SayHi(string text) { }
}

class C
{
    void M()
    {
        var greeter = new Greeter();
        greeter.SayHi($$
    }
}";

            var (items, _) = await RequestCompletionAsync(fileName, markup);
            Assert.Contains(items, item => item.DisplayText == "text:");
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Returns_override_signatures(string fileName)
        {
            const string markup = @"
class B
{
    public virtual void Test(string text) { }
    public virtual void Test(string text, string moreText) { }
}

class C : B
{
    override $$
}";

            var (items, _) = await RequestCompletionAsync(fileName, markup);

            Assert.Contains(items, item => item.DisplayText == "Equals(object obj)");
            Assert.Contains(items, item => item.DisplayText == "GetHashCode()");
            Assert.Contains(items, item => item.DisplayText == "Test(string text)");
            Assert.Contains(items, item => item.DisplayText == "Test(string text, string moreText)");
            Assert.Contains(items, item => item.DisplayText == "ToString()");
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Returns_cref_completion(string fileName)
        {
            const string markup = @"
/// <summary>
/// A comment. <see cref=""My$$"" /> for more details
/// </summary>
public class MyClass
{
}
";

            var (items, _) = await RequestCompletionAsync(fileName, markup);
            Assert.Contains(items, item => item.DisplayText == "MyClass");
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Returns_cref_completion_for_generic(string fileName)
        {
            const string markup = @"
/// <summary>
/// A comment. <see cref=""System.Collections.Generic.$$"" /> for more details
/// </summary>
public class MyClass
{
}
";

            var (items, _) = await RequestCompletionAsync(fileName, markup);
            Assert.Contains(items, item => item.DisplayText == "List{T}");
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
}";

            await AssertItemDescriptionAsync(fileName, markup,
                displayText: "B", expectedDescription:
                "class B\r\nMy Crefs are StringBuilder and System.IO.Path.");
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Is_suggestion_mode_true_for_lambda_expression_position1(string fileName)
        {
            const string markup = @"
using System;
class C
{
    int CallMe(int i) => 42;

    void M(Func<int, int> a) { }

    void M()
    {
        M(c$$
    }
}";

            var (_, isSuggestionMode) = await RequestCompletionAsync(fileName, markup);
            Assert.True(isSuggestionMode);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Is_suggestion_mode_true_for_lambda_expression_position2(string fileName)
        {
            const string markup = @"
using System;
class C
{
    int CallMe(int i) => 42;

    void M()
    {
        Func<int, int> a = c$$
    }
}";

            var (_, isSuggestionMode) = await RequestCompletionAsync(fileName, markup);
            Assert.True(isSuggestionMode);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Is_suggestion_mode_false_for_normal_position1(string fileName)
        {
            const string markup = @"
using System;
class C
{
    int CallMe(int i) => 42;

    void M(int a) { }

    void M()
    {
        M(c$$
    }
}";

            var (_, isSuggestionMode) = await RequestCompletionAsync(fileName, markup);
            Assert.False(isSuggestionMode);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Is_suggestion_mode_false_for_normal_position2(string fileName)
        {
            const string markup = @"
using System;
class C
{
    int CallMe(int i) => 42;

    void M()
    {
        int a = c$$
    }
}";

            var (_, isSuggestionMode) = await RequestCompletionAsync(fileName, markup);
            Assert.False(isSuggestionMode);
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
