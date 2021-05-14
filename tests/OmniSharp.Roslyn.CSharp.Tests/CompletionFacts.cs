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
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CompletionFacts : AbstractTestFixture
    {
        private const int ImportCompletionTimeout = 1000;
        private readonly ILogger _logger;

        private string EndpointName => OmniSharpEndpoints.Completion;

        public CompletionFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
            this._logger = this.LoggerFactory.CreateLogger<CompletionFacts>();
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PropertyCompletion(string filename)
        {
            const string input =
                @"public class Class1 {
                    public int Foo { get; set; }
                    public Class1()
                        {
                            Foo$$
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.Contains("Foo", completions.Items.Select(c => c.Label));
            Assert.Contains("Foo", completions.Items.Select(c => c.TextEdit.NewText));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task VariableCompletion(string filename)
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            var foo = 1;
                            foo$$
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.Contains("foo", completions.Items.Select(c => c.Label));
            Assert.Contains("foo", completions.Items.Select(c => c.TextEdit.NewText));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ParameterCompletion(string filename)
        {
            const string input =
                @"public class Class1 {
                    public Class1(string foo)
                        {
                            foo$$
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.Contains("foo", completions.Items.Select(c => c.Label));
            Assert.Contains("foo", completions.Items.Select(c => c.TextEdit.NewText));
            Assert.Equal(CompletionItemKind.Variable, completions.Items.First(c => c.TextEdit.NewText == "foo").Kind);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task DocumentationIsResolved(string filename)
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            Foo$$
                        }
                    /// <summary>Some Text</summary>
                    public void Foo(int bar = 1)
                        {
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.All(completions.Items, c => Assert.Null(c.Documentation));

            var fooCompletion = completions.Items.Single(c => c.Label == "Foo");
            var resolvedCompletion = await ResolveCompletionAsync(fooCompletion, SharedOmniSharpTestHost);
            Assert.Equal("```csharp\nvoid Class1.Foo([int bar = 1])\n```\n\nSome Text", resolvedCompletion.Item.Documentation);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsCamelCasedCompletions(string filename)
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Guid.tp$$
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.Contains("TryParse", completions.Items.Select(c => c.TextEdit.NewText));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ImportCompletionTurnedOff(string filename)
        {
            const string input =
@"public class Class1 {
    public Class1()
    {
        Gui$$
    }
}";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.False(completions.IsIncomplete);
            Assert.DoesNotContain("Guid", completions.Items.Select(c => c.TextEdit.NewText));
        }

        [Theory]
        [InlineData("dummy.cs", true)]
        [InlineData("dummy.cs", false)]
        [InlineData("dummy.csx", true)]
        [InlineData("dummy.csx", false)]
        public async Task ImportCompletionResolvesOnSubsequentQueries(string filename, bool useAsyncCompletion)
        {
            const string input =
@"public class Class1 {
    public Class1()
    {
        Gui$$
    }
}";

            using var host = useAsyncCompletion ? GetAsyncCompletionAndImportCompletionHost() : GetImportCompletionHost();

            // First completion request should kick off the task to update the completion cache.
            var completions = await FindCompletionsAsync(filename, input, host);
            Assert.True(completions.IsIncomplete);
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
        [InlineData("dummy.cs", true)]
        [InlineData("dummy.cs", false)]
        [InlineData("dummy.csx", true)]
        [InlineData("dummy.csx", false)]
        public async Task ImportCompletion_LocalsPrioritizedOverImports(string filename, bool useAsyncCompletion)
        {

            const string input =
@"public class Class1 {
    public Class1()
    {
        string guid;
        Gui$$
    }
}";

            using var host = useAsyncCompletion ? GetAsyncCompletionAndImportCompletionHost() : GetImportCompletionHost();
            var completions = await FindCompletionsWithImportedAsync(filename, input, host);
            CompletionItem localCompletion = completions.Items.First(c => c.TextEdit.NewText == "guid");
            CompletionItem typeCompletion = completions.Items.First(c => c.TextEdit.NewText == "Guid");
            Assert.True(localCompletion.Data.Index < typeCompletion.Data.Index);
            Assert.StartsWith("0", localCompletion.SortText);
            Assert.StartsWith("1", typeCompletion.SortText);
            VerifySortOrders(completions.Items);
        }

        [Theory]
        [InlineData("dummy.cs", true)]
        [InlineData("dummy.cs", false)]
        [InlineData("dummy.csx", true)]
        [InlineData("dummy.csx", false)]
        public async Task ImportCompletions_IncludesExtensionMethods(string filename, bool useAsyncCompletion)
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

            using var host = useAsyncCompletion ? GetAsyncCompletionAndImportCompletionHost() : GetImportCompletionHost();
            var completions = await FindCompletionsWithImportedAsync(filename, input, host);
            Assert.Contains("Test", completions.Items.Select(c => c.TextEdit.NewText));
            VerifySortOrders(completions.Items);
        }

        [Theory]
        [InlineData("dummy.cs", true)]
        [InlineData("dummy.cs", false)]
        [InlineData("dummy.csx", true)]
        [InlineData("dummy.csx", false)]
        public async Task ImportCompletion_ResolveAddsImportEdit(string filename, bool useAsyncCompletion)
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

            using var host = useAsyncCompletion ? GetAsyncCompletionAndImportCompletionHost() : GetImportCompletionHost();
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
        [InlineData("dummy.cs", true)]
        [InlineData("dummy.cs", false)]
        [InlineData("dummy.csx", true)]
        [InlineData("dummy.csx", false)]
        public async Task ImportCompletion_OnLine0(string filename, bool useAsyncCompletion)
        {
            const string input = @"$$";

            using var host = useAsyncCompletion ? GetAsyncCompletionAndImportCompletionHost() : GetImportCompletionHost();
            var completions = await FindCompletionsWithImportedAsync(filename, input, host);
            var resolved = await ResolveCompletionAsync(completions.Items.First(c => c.TextEdit.NewText == "Console"), host);

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
        [InlineData("dummy.cs", true)]
        [InlineData("dummy.cs", false)]
        [InlineData("dummy.csx", true)]
        [InlineData("dummy.csx", false)]
        public async Task SelectsLastInstanceOfCompletion(string filename, bool useAsyncCompletion)
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

            using var host = useAsyncCompletion ? GetAsyncCompletionAndImportCompletionHost() : GetImportCompletionHost();
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
        [InlineData("dummy.cs", true)]
        [InlineData("dummy.cs", false)]
        [InlineData("dummy.csx", true)]
        [InlineData("dummy.csx", false)]
        public async Task UsingsAddedInOrder(string filename, bool useAsyncCompletion)
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

            using var host = useAsyncCompletion ? GetAsyncCompletionAndImportCompletionHost() : GetImportCompletionHost();
            var completions = await FindCompletionsWithImportedAsync(filename, input, host);
            var resolved = await ResolveCompletionAsync(completions.Items.First(c => c.TextEdit.NewText == "C2"), host);

            Assert.Single(resolved.Item.AdditionalTextEdits);
            var additionalEdit = resolved.Item.AdditionalTextEdits[0];
            Assert.Equal(NormalizeNewlines("N2;\nusing "),
                         additionalEdit.NewText);
            Assert.Equal(1, additionalEdit.StartLine);
            Assert.Equal(6, additionalEdit.StartColumn);
            Assert.Equal(1, additionalEdit.EndLine);
            Assert.Equal(6, additionalEdit.EndColumn);
            VerifySortOrders(completions.Items);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsSubsequences(string filename)
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Guid.ng$$
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.Contains("NewGuid", completions.Items.Select(c => c.Label));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsSubsequencesWithoutFirstLetter(string filename)
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Guid.gu$$
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.Contains("NewGuid", completions.Items.Select(c => c.Label));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task MethodHeaderDocumentation(string filename)
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Guid.ng$$
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.All(completions.Items, c => Assert.Null(c.Documentation));

            var fooCompletion = completions.Items.Single(c => c.Label == "NewGuid");
            var resolvedCompletion = await ResolveCompletionAsync(fooCompletion, SharedOmniSharpTestHost);
            Assert.Equal("```csharp\nSystem.Guid System.Guid.NewGuid()\n```", resolvedCompletion.Item.Documentation);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PreselectsCorrectCasing_Lowercase(string filename)
        {
            const string input =
                @"public class MyClass1 {

                    public MyClass1()
                        {
                            var myvar = 1;
                            my$$
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.Contains(completions.Items, c => c.Label == "myvar");
            Assert.Contains(completions.Items, c => c.Label == "MyClass1");
            Assert.All(completions.Items, c => Assert.False(c.Preselect));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PreselectsCorrectCasing_Uppercase(string filename)
        {
            const string input =
                @"public class MyClass1 {

                    public MyClass1()
                        {
                            var myvar = 1;
                            My$$
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.Contains(completions.Items, c => c.Label == "myvar");
            Assert.Contains(completions.Items, c => c.Label == "MyClass1");
            Assert.All(completions.Items, c => Assert.False(c.Preselect));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task NoCompletionsInInvalid(string filename)
        {
            const string source =
                @"public class MyClass1 {

                    public MyClass1()
                        {
                            var x$$
                        }
                    }";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Empty(completions.Items);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task AttributeDoesNotHaveAttributeSuffix(string filename)
        {
            const string source =
                @"using System;

                    public class BarAttribute : Attribute {}

                    [B$$
                    public class Foo {}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Contains(completions.Items, c => c.Label == "Bar");
            Assert.Contains(completions.Items, c => c.TextEdit.NewText == "Bar");
            Assert.All(completions.Items, c => Assert.False(c.Preselect));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsObjectInitalizerMembers(string filename)
        {
            const string source =
                @"public class MyClass1 {
                        public string Foo {get; set;}
                  }

                    public class MyClass2 {

                        public MyClass2()
                        {
                            var c = new MyClass1 {
                             F$$
                        }
                    }
                ";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Single(completions.Items);
            Assert.Equal("Foo", completions.Items[0].Label);
            Assert.Equal("Foo", completions.Items[0].TextEdit.NewText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task IncludesParameterNames(string filename)
        {
            const string source =
                @"public class MyClass1 {
                        public void SayHi(string text) {}
                  }

                    public class MyClass2 {

                        public MyClass2()
                        {
                            var c = new MyClass1();
                            c.SayHi(te$$
                        }
                    }
                ";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            var item = completions.Items.First(c => c.Label == "text:");
            Assert.NotNull(item);
            Assert.Equal("text", item.TextEdit.NewText);
            Assert.All(completions.Items, c =>
            {
                if (c.Label == "ToString")
                    Assert.True(c.Preselect);
                else
                    Assert.False(c.Preselect);
            });
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsNameSuggestions(string filename)
        {
            const string source =
                @"
public class MyClass
{
    MyClass m$$
}
                ";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "myClass", "my", "@class", "MyClass", "My", "Class", "GetMyClass", "GetMy", "GetClass" },
                         completions.Items.Select(c => c.Label));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_Publics_Async(string filename)
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

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "Test(string text)", "Test(string text, string moreText)", "ToString()" },
                         completions.Items.Select(c => c.Label));
            Assert.Equal(new[] { "Equals", "GetHashCode", "Test", "Test", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(8, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(8, change.EndLine);
            Assert.Equal(19, change.EndColumn);
            Assert.Equal(10, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_Publics_Sync(string filename)
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

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "Test(string text)", "Test(string text, string moreText)", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "public override void Test(string text)\n    {\n        base.Test(text);$0\n    \\}",
                                 "public override void Test(string text, string moreText)\n    {\n        base.Test(text, moreText);$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                                },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override Test",
                                 "override Test",
                                 "override ToString"
                                },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));

            Assert.All(completions.Items,
                       c =>
                       {
                           Assert.Equal(9, c.TextEdit.StartLine);
                           Assert.Equal(4, c.TextEdit.StartColumn);
                           Assert.Equal(9, c.TextEdit.EndLine);
                           Assert.Equal(13, c.TextEdit.EndColumn);
                       });

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_UnimportedTypesFullyQualified_Async(string filename)
        {
            const string source = @"
using N2;
namespace N1
{
    public class CN1 {}
}
namespace N2
{
    using N1;
    public abstract class IN2 { protected abstract CN1 GetN1(); }
}
namespace N3
{
    class CN3 : IN2
    {
        override $$
    }
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "GetN1()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals", "GetHashCode", "GetN1", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items.First(i => i.Label == "GetN1()"), host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("protected override N1.CN1 GetN1()\n        {\n            throw new System.NotImplementedException();\n        }",
                         change.NewText);
            Assert.Equal(15, change.StartLine);
            Assert.Equal(8, change.StartColumn);
            Assert.Equal(15, change.EndLine);
            Assert.Equal(22, change.EndColumn);
            Assert.Equal(17, afterInsert.Line);
            Assert.Equal(55, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_UnimportedTypesFullyQualified_Sync(string filename)
        {
            const string source = @"
using N2;
namespace N1
{
    public class CN1 {}
}
namespace N2
{
    using N1;
    public abstract class IN2 { protected abstract CN1 GetN1(); }
}
namespace N3
{
    class CN3 : IN2
    {
        override $$
    }
}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "GetN1()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n        {\n            return base.Equals(obj);$0\n        \\}",
                                 "public override int GetHashCode()\n        {\n            return base.GetHashCode();$0\n        \\}",
                                 "protected override N1.CN1 GetN1()\n        {\n            throw new System.NotImplementedException();$0\n        \\}",
                                 "public override string ToString()\n        {\n            return base.ToString();$0\n        \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override GetN1",
                                 "override ToString"
                               },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items,
                       c =>
                       {
                           Assert.Equal(15, c.TextEdit.StartLine);
                           Assert.Equal(8, c.TextEdit.StartColumn);
                           Assert.Equal(15, c.TextEdit.EndLine);
                           Assert.Equal(17, c.TextEdit.EndColumn);
                       });

            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_ModifierInFront_Async(string filename)
        {
            const string source = @"
class C
{
    public override $$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals", "GetHashCode", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(3, change.StartLine);
            Assert.Equal(20, change.StartColumn);
            Assert.Equal(3, change.EndLine);
            Assert.Equal(26, change.EndColumn);
            Assert.Equal(5, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_ModifierInFront_Sync(string filename)
        {
            const string source = @"
class C
{
    public override $$
}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "Equals",
                                 "GetHashCode",
                                 "ToString"
                               },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_PartiallyTypedMethod_Async(string filename)
        {
            const string source = @"
class C
{
    override Eq$$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals", "GetHashCode", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));
            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(3, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(3, change.EndLine);
            Assert.Equal(19, change.EndColumn);
            Assert.Equal(5, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_PartiallyTypedMethod_Sync(string filename)
        {
            const string source = @"
class C
{
    override Eq$$
}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.NewText));
            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_ModifierAndReturnTypeInFront_Async(string filename)
        {
            const string source = @"
class C
{
    public override bool $$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object obj)" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(3, change.StartLine);
            Assert.Equal(31, change.StartColumn);
            Assert.Equal(3, change.EndLine);
            Assert.Equal(31, change.EndColumn);
            Assert.Equal(5, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_ModifierAndReturnTypeInFront_Sync(string filename)
        {
            const string source = @"
class C
{
    public override bool $$
}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object obj)" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "Equals" },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_TestTest_Async(string filename)
        {
            const string source = @"
class Test {}
abstract class Base
{
    protected abstract Test Test();
}
class Derived : Base
{
    override $$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "Test()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals", "GetHashCode", "Test", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(8, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(8, change.EndLine);
            Assert.Equal(19, change.EndColumn);
            Assert.Equal(10, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_TestTest_Sync(string filename)
        {
            const string source = @"
class Test {}
abstract class Base
{
    protected abstract Test Test();
}
class Derived : Base
{
    override $$
}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "Test()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "protected override Test Test()\n    {\n        throw new System.NotImplementedException();$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override Test",
                                 "override ToString"
                               },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items,
                       c =>
                       {
                           Assert.Equal(8, c.TextEdit.StartLine);
                           Assert.Equal(4, c.TextEdit.StartColumn);
                           Assert.Equal(8, c.TextEdit.EndLine);
                           Assert.Equal(13, c.TextEdit.EndColumn);
                       });

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));
        }

        [Fact]
        public async Task OverrideCompletion_TypesNeedImport_Async()
        {
            const string baseText = @"
using System;
public class Base
{
    public virtual Action GetAction(Action a) => null;
}
";

            const string derivedText = @"
public class Derived : Base
{
    override $$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync("derived.cs", derivedText, host, additionalFiles: new[] { new TestFile("base.cs", baseText) });
            var item = completions.Items.Single(c => c.Label.StartsWith("GetAction"));
            Assert.Equal("GetAction(System.Action a)", item.Label);
            Assert.Equal("GetAction", item.TextEdit.NewText);

            Assert.Null(item.AdditionalTextEdits);

            var afterInsert = await AfterInsertResponse(item, host);
            Assert.Equal(2, afterInsert.Changes.Count);

            var firstChange = afterInsert.Changes[0];
            Assert.Equal(NormalizeNewlines("using System;\n\n"), firstChange.NewText);
            Assert.Equal(1, firstChange.StartLine);
            Assert.Equal(0, firstChange.StartColumn);
            Assert.Equal(1, firstChange.EndLine);
            Assert.Equal(0, firstChange.EndColumn);

            var secondChange = afterInsert.Changes[1];
            Assert.Equal(NormalizeNewlines("public override Action GetAction(Action a)\n    {\n        return base.GetAction(a);\n    }"), secondChange.NewText);
            Assert.Equal(3, secondChange.StartLine);
            Assert.Equal(4, secondChange.StartColumn);
            Assert.Equal(3, secondChange.EndLine);
            Assert.Equal(22, secondChange.EndColumn);

            Assert.Equal(7, afterInsert.Line);
            Assert.Equal(33, afterInsert.Column);
        }

        [Fact]
        public async Task OverrideCompletion_TypesNeedImport_Sync()
        {
            const string baseText = @"
using System;
public class Base
{
    public virtual Action GetAction(Action a) => null;
}
";

            const string derivedText = @"
public class Derived : Base
{
    override $$
}";

            var completions = await FindCompletionsAsync("derived.cs", derivedText, SharedOmniSharpTestHost, additionalFiles: new[] { new TestFile("base.cs", baseText) });
            var item = completions.Items.Single(c => c.Label.StartsWith("GetAction"));
            Assert.Equal("GetAction(System.Action a)", item.Label);

            Assert.Single(item.AdditionalTextEdits);
            Assert.Equal(NormalizeNewlines("using System;\n\n"), item.AdditionalTextEdits[0].NewText);
            Assert.Equal(1, item.AdditionalTextEdits[0].StartLine);
            Assert.Equal(0, item.AdditionalTextEdits[0].StartColumn);
            Assert.Equal(1, item.AdditionalTextEdits[0].EndLine);
            Assert.Equal(0, item.AdditionalTextEdits[0].EndColumn);
            Assert.Equal(NormalizeNewlines("public override Action GetAction(Action a)\n    {\n        return base.GetAction(a);$0\n    \\}"), item.TextEdit.NewText);
            Assert.Equal(3, item.TextEdit.StartLine);
            Assert.Equal(4, item.TextEdit.StartColumn);
            Assert.Equal(3, item.TextEdit.EndLine);
            Assert.Equal(13, item.TextEdit.EndColumn);
            Assert.Equal("override GetAction", item.FilterText);
        }

        [Fact]
        public async Task OverrideCompletion_FromNullableToNonNullableContext_Async()
        {
            const string text = @"
#nullable enable
public class Base
{
    public virtual object? M1(object? param) => throw null;
}
#nullable disable
public class Derived : Base
{
    override $$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync("derived.cs", text, host);
            var item = completions.Items.Single(c => c.Label.StartsWith("M1"));
            Assert.Equal("M1(object? param)", item.Label);
            Assert.Equal("M1", item.TextEdit.NewText);

            Assert.Null(item.AdditionalTextEdits);

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(9, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(9, change.EndLine);
            Assert.Equal(19, change.EndColumn);
            Assert.Equal(11, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Fact]
        public async Task OverrideCompletion_FromNullableToNonNullableContext_Sync()
        {
            const string text = @"
#nullable enable
public class Base
{
    public virtual object? M1(object? param) => throw null;
}
#nullable disable
public class Derived : Base
{
    override $$
}";

            var completions = await FindCompletionsAsync("derived.cs", text, SharedOmniSharpTestHost);
            var item = completions.Items.Single(c => c.Label.StartsWith("M1"));
            Assert.Equal("M1(object? param)", item.Label);

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal(9, item.TextEdit.StartLine);
            Assert.Equal(4, item.TextEdit.StartColumn);
            Assert.Equal(9, item.TextEdit.EndLine);
            Assert.Equal(13, item.TextEdit.EndColumn);
            Assert.Equal("public override object M1(object param)\n    {\n        return base.M1(param);$0\n    \\}", item.TextEdit.NewText);
            Assert.Equal("override M1", item.FilterText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideCompletion_PropertyGetSet_Sync(string filename)
        {
            const string source = @"
using System;
public class Base
{
    public abstract string Prop { get; set; }
}
public class Derived : Base
{
    override $$
}
";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            var item = completions.Items.Single(c => c.Label.StartsWith("Prop"));
            Assert.Equal("Prop", item.Label);

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal(8, item.TextEdit.StartLine);
            Assert.Equal(4, item.TextEdit.StartColumn);
            Assert.Equal(8, item.TextEdit.EndLine);
            Assert.Equal(13, item.TextEdit.EndColumn);
            Assert.Equal("public override string Prop { get => throw new NotImplementedException()$0; set => throw new NotImplementedException(); \\}", item.TextEdit.NewText);
            Assert.Equal("override Prop", item.FilterText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideCompletion_PropertyGetSet_Async(string filename)
        {
            const string source = @"
using System;
public class Base
{
    public abstract string Prop { get; set; }
}
public class Derived : Base
{
    override $$
}
";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            var item = completions.Items.Single(c => c.Label.StartsWith("Prop"));
            Assert.Equal("Prop", item.Label);
            Assert.Equal("Prop", item.TextEdit.NewText);

            Assert.Null(item.AdditionalTextEdits);

            var afterInsert = await AfterInsertResponse(item, host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override string Prop { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }", change.NewText);
            Assert.Equal(8, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(8, change.EndLine);
            Assert.Equal(17, change.EndColumn);
            Assert.Equal(8, afterInsert.Line);
            Assert.Equal(76, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideCompletion_PropertyGet_Sync(string filename)
        {
            const string source = @"
using System;
public class Base
{
    public abstract string Prop { get; }
}
public class Derived : Base
{
    override $$
}
";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            var item = completions.Items.Single(c => c.Label.StartsWith("Prop"));
            Assert.Equal("Prop", item.Label);

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal(8, item.TextEdit.StartLine);
            Assert.Equal(4, item.TextEdit.StartColumn);
            Assert.Equal(8, item.TextEdit.EndLine);
            Assert.Equal(13, item.TextEdit.EndColumn);
            Assert.Equal("public override string Prop => throw new NotImplementedException();", item.TextEdit.NewText);
            Assert.Equal(InsertTextFormat.PlainText, item.InsertTextFormat);
            Assert.Equal("override Prop", item.FilterText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PartialCompletion_Async(string filename)
        {
            const string source = @"
partial class C
{
    partial void M1(string param);
}
partial class C
{
    partial $$
}
";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "M1(string param)" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "M1" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("void M1(string param)\n    {\n        throw new System.NotImplementedException();\n    }", change.NewText);
            Assert.Equal(7, change.StartLine);
            Assert.Equal(12, change.StartColumn);
            Assert.Equal(7, change.EndLine);
            Assert.Equal(14, change.EndColumn);
            Assert.Equal(9, afterInsert.Line);
            Assert.Equal(51, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PartialCompletion_Sync(string filename)
        {
            const string source = @"
partial class C
{
    partial void M1(string param);
}
partial class C
{
    partial $$
}
";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "M1(string param)" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "void M1(string param)\n    {\n        throw new System.NotImplementedException();$0\n    \\}" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Fact]
        public async Task PartialCompletion_TypesNeedImport_Async()
        {
            const string file1 = @"
using System;
public partial class C
{
    partial void M(Action a);
}
";

            const string file2 = @"
public partial class C
{
    partial $$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync("derived.cs", file2, host, additionalFiles: new[] { new TestFile("base.cs", file1) });
            var item = completions.Items.Single(c => c.Label.StartsWith("M"));

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal("M", item.TextEdit.NewText);

            var afterInsert = await AfterInsertResponse(item, host);
            Assert.Equal(2, afterInsert.Changes.Count);

            var firstChange = afterInsert.Changes[0];
            Assert.Equal(NormalizeNewlines("using System;\n\n"), firstChange.NewText);
            Assert.Equal(1, firstChange.StartLine);
            Assert.Equal(0, firstChange.StartColumn);
            Assert.Equal(1, firstChange.EndLine);
            Assert.Equal(0, firstChange.EndColumn);

            var secondChange = afterInsert.Changes[1];
            Assert.Equal(NormalizeNewlines("void M(Action a)\n    {\n        throw new NotImplementedException();\n    }"), secondChange.NewText);
            Assert.Equal(3, secondChange.StartLine);
            Assert.Equal(12, secondChange.StartColumn);
            Assert.Equal(3, secondChange.EndLine);
            Assert.Equal(13, secondChange.EndColumn);

            Assert.Equal(7, afterInsert.Line);
            Assert.Equal(44, afterInsert.Column);
        }

        [Fact]
        public async Task PartialCompletion_TypesNeedImport_Sync()
        {
            const string file1 = @"
using System;
public partial class C
{
    partial void M(Action a);
}
";

            const string file2 = @"
public partial class C
{
    partial $$
}";

            var completions = await FindCompletionsAsync("derived.cs", file2, SharedOmniSharpTestHost, additionalFiles: new[] { new TestFile("base.cs", file1) });
            var item = completions.Items.Single(c => c.Label.StartsWith("M"));

            Assert.Single(item.AdditionalTextEdits);
            Assert.Equal(NormalizeNewlines("using System;\n\n"), item.AdditionalTextEdits[0].NewText);
            Assert.Equal(1, item.AdditionalTextEdits[0].StartLine);
            Assert.Equal(0, item.AdditionalTextEdits[0].StartColumn);
            Assert.Equal(1, item.AdditionalTextEdits[0].EndLine);
            Assert.Equal(0, item.AdditionalTextEdits[0].EndColumn);

            Assert.Equal("void M(Action a)\n    {\n        throw new NotImplementedException();$0\n    \\}", item.TextEdit.NewText);
            Assert.Equal("M", item.FilterText);
            Assert.Equal(3, item.TextEdit.StartLine);
            Assert.Equal(12, item.TextEdit.StartColumn);
            Assert.Equal(3, item.TextEdit.EndLine);
            Assert.Equal(12, item.TextEdit.EndColumn);
        }

        [Fact]
        public async Task PartialCompletion_FromNullableToNonNullableContext_Async()
        {
            const string text = @"
#nullable enable
public partial class C
{
    partial void M1(object? param);
}
#nullable disable
public partial class C
{
    partial $$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync("derived.cs", text, host);
            var item = completions.Items.Single(c => c.Label.StartsWith("M1"));
            Assert.Equal("M1(object param)", item.Label);
            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal("M1", item.TextEdit.NewText);

            var afterInsert = await AfterInsertResponse(item, host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("void M1(object param)\n    {\n        throw new System.NotImplementedException();\n    }", change.NewText);
            Assert.Equal(9, change.StartLine);
            Assert.Equal(12, change.StartColumn);
            Assert.Equal(9, change.EndLine);
            Assert.Equal(14, change.EndColumn);
            Assert.Equal(11, afterInsert.Line);
            Assert.Equal(51, afterInsert.Column);
        }

        [Fact]
        public async Task PartialCompletion_FromNullableToNonNullableContext_Sync()
        {
            const string text = @"
#nullable enable
public partial class C
{
    partial void M1(object? param);
}
#nullable disable
public partial class C
{
    partial $$
}";

            var completions = await FindCompletionsAsync("derived.cs", text, SharedOmniSharpTestHost);
            var item = completions.Items.Single(c => c.Label.StartsWith("M1"));
            Assert.Equal("M1(object param)", item.Label);
            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal("void M1(object param)\n    {\n        throw new System.NotImplementedException();$0\n    \\}", item.TextEdit.NewText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_PartiallyTypedIdentifier_Async(string filename)
        {
            const string source = @"
class C
{
    override Ge$$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals", "GetHashCode", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(3, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(3, change.EndLine);
            Assert.Equal(19, change.EndColumn);
            Assert.Equal(5, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_PartiallyTypedIdentifier_Sync_Sync(string filename)
        {
            const string source = @"
class C
{
    override Ge$$
}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override ToString"
                               },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));

            Assert.All(completions.Items.Select(c => c.TextEdit),
                       r =>
                       {
                           Assert.Equal(3, r.StartLine);
                           Assert.Equal(4, r.StartColumn);
                           Assert.Equal(3, r.EndLine);
                           Assert.Equal(15, r.EndColumn);
                       });

            Assert.All(completions.Items, c => Assert.False(c.Preselect));

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CrefCompletion(string filename)
        {
            const string source =
                @"  /// <summary>
                    /// A comment. <see cref=""My$$"" /> for more details
                    /// </summary>
                  public class MyClass1 {
                  }
                ";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Contains(completions.Items, c => c.Label == "MyClass1");
            Assert.All(completions.Items, c =>
            {
                switch (c.Label)
                {
                    default:
                        Assert.False(c.Preselect);
                        break;
                }
            });
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task DocCommentTagCompletions(string filename)
        {
            const string source =
                @"  /// <summary>
                    /// A comment. <$$
                    /// </summary>
                  public class MyClass1 {
                  }
                ";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "<!--$0-->",
                                 "<![CDATA[$0]]>",
                                 "c",
                                 "code",
                                 "<inheritdoc$0/>",
                                 "<list type=\"$0\"",
                                 "para",
                                 "<see cref=\"$0\"/>",
                                 "<seealso cref=\"$0\"/>"
                         },
                         completions.Items.Select(c => c.TextEdit.NewText));
            Assert.All(completions.Items, c =>
            {
                if (c.InsertTextFormat == InsertTextFormat.Snippet)
                {
                    Assert.Equal(35, c.TextEdit.StartColumn);
                    Assert.Contains("0", c.TextEdit.NewText);
                    Assert.StartsWith("<", c.FilterText);
                }
                else
                {
                    Assert.Equal(36, c.TextEdit.StartColumn);
                    Assert.DoesNotContain("0", c.TextEdit.NewText);
                    Assert.Null(c.FilterText);
                }
            });
        }

        [Fact]
        public async Task HostObjectCompletionInScripts()
        {
            const string source =
                "Prin$$";

            var completions = await FindCompletionsAsync("dummy.csx", source, SharedOmniSharpTestHost);
            Assert.Contains(completions.Items, c => c.Label == "Print");
            Assert.Contains(completions.Items, c => c.Label == "PrintOptions");
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task NoCommitOnSpaceInLambdaParameter_MethodArgument(string filename)
        {
            const string source = @"
using System;
class C
{
    int CallMe(int i) => 42;

    void M(Func<int, int> a) { }
    void M(string unrelated) { }

    void M()
    {
        M(c$$
    }
}
";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);

            Assert.True(completions.Items.All(c => c.IsSuggestionMode()));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task NoCommitOnSpaceInLambdaParameter_Initializer(string filename)
        {
            const string source = @"
using System;
class C
{
    int CallMe(int i) => 42;

    void M()
    {
        Func<int, int> a = c$$
    }
}
";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);

            Assert.True(completions.Items.All(c => c.IsSuggestionMode()));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CommitOnSpaceWithoutLambda_InArgument(string filename)
        {
            const string source = @"
using System;
class C
{
    int CallMe(int i) => 42;

    void M(int a) { }

    void M()
    {
        M(c$$
    }
}
";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);

            Assert.True(completions.Items.All(c => !c.IsSuggestionMode()));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CommitOnSpaceWithoutLambda_InInitializer(string filename)
        {
            const string source = @"
using System;
class C
{
    int CallMe(int i) => 42;

    void M()
    {
        int a = c$$
    }
}
";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);

            Assert.True(completions.Items.All(c => !c.IsSuggestionMode()));
        }

        [Fact]
        public async Task ScriptingIncludes7_1()
        {
            const string source =
                @"
                  var number1 = 1;
                  var number2 = 2;
                  var tuple = (number1, number2);
                  tuple.n$$
                ";

            var completions = await FindCompletionsAsync("dummy.csx", source, SharedOmniSharpTestHost);
            Assert.Contains(completions.Items, c => c.Label == "number1");
            Assert.Contains(completions.Items, c => c.Label == "number2");
            Assert.All(completions.Items, c => Assert.False(c.Preselect));
        }

        [Fact]
        public async Task ScriptingIncludes7_2()
        {
            const string source =
                @"
                  public class Foo { private protected int myValue = 0; }
                  public class Bar : Foo
                  {
                    public Bar()
                    {
                        var x = myv$$
                    }
                  }
                ";

            var completions = await FindCompletionsAsync("dummy.csx", source, SharedOmniSharpTestHost);
            Assert.Contains(completions.Items, c => c.Label == "myValue");
            Assert.All(completions.Items, c =>
            Assert.False(c.Preselect));
        }

        [Fact]
        public async Task ScriptingIncludes8_0()
        {
            const string source =
                @"
                  class Point {
                    public Point(int x, int y) {
                      PositionX = x;
                      PositionY = y;
                    }
                    public int PositionX { get; }
                    public int PositionY { get; }
                  }
                  Point[] points = { new (1, 2), new (3, 4) };
                  points[0].Po$$
                ";

            var completions = await FindCompletionsAsync("dummy.csx", source, SharedOmniSharpTestHost);
            Assert.Contains(completions.Items, c => c.Label == "PositionX");
            Assert.Contains(completions.Items, c => c.Label == "PositionY");
            Assert.All(completions.Items, c => Assert.False(c.Preselect));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task TriggeredOnSpaceForObjectCreation(string filename)
        {
            const string input =
@"public class Class1 {
    public M()
    {
        Class1 c = new $$
    }
}";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost, triggerChar: ' ');
            Assert.NotEmpty(completions.Items);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsAtleastOnePreselectOnNew(string filename)
        {
            const string input =
@"public class Class1 {
    public M()
    {
        Class1 c = new $$
    }
}";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost, triggerChar: ' ');
            Assert.NotEmpty(completions.Items.Where(completion => completion.Preselect == true));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task NotTriggeredOnSpaceWithoutObjectCreation(string filename)
        {
            const string input =
@"public class Class1 {
    public M()
    {
        $$
    }
}";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost, triggerChar: ' ');
            Assert.Empty(completions.Items);
        }

        [Fact]
        public async Task InternalsVisibleToCompletion()
        {
            var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "ProjectNameVal",
                "AssemblyNameVal",
                LanguageNames.CSharp,
                "/path/to/project.csproj");

            SharedOmniSharpTestHost.Workspace.AddProject(projectInfo);

            const string input = @"
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""$$";

            var completions = await FindCompletionsAsync("dummy.cs", input, SharedOmniSharpTestHost);
            Assert.Single(completions.Items);
            Assert.Equal("AssemblyNameVal", completions.Items[0].Label);
            Assert.Equal("AssemblyNameVal", completions.Items[0].TextEdit.NewText);
        }

        [Fact]
        public async Task InternalsVisibleToCompletionSkipsMiscProject()
        {
            var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "ProjectNameVal",
                "AssemblyNameVal",
                LanguageNames.CSharp,
                "/path/to/project.csproj");

            SharedOmniSharpTestHost.Workspace.AddProject(projectInfo);

            var miscFile = "class Foo {}";
            var miscFileLoader = TextLoader.From(TextAndVersion.Create(SourceText.From(miscFile), VersionStamp.Create()));
            SharedOmniSharpTestHost.Workspace.TryAddMiscellaneousDocument("dummy.cs", miscFileLoader, LanguageNames.CSharp);

            const string input = @"
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""$$";

            var completions = await FindCompletionsAsync("dummy.cs", input, SharedOmniSharpTestHost);
            Assert.Single(completions.Items);
            Assert.Equal("AssemblyNameVal", completions.Items[0].Label);
            Assert.Equal("AssemblyNameVal", completions.Items[0].TextEdit.NewText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PrefixHeaderIsFullyCorrect_Async(string filename)
        {
            const string input =
@"public class Base
{
    protected virtual void OnEnable() {}
}
public class Derived : Base
{
    protected override void On$$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, input, host);
            var onEnable = completions.Items.Single(c => c.TextEdit.NewText.Contains("OnEnable"));

            Assert.Equal("OnEnable", onEnable.TextEdit.NewText);

            var afterInsert = await AfterInsertResponse(onEnable, host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("()\n    {\n        base.OnEnable();\n    }", change.NewText);
            Assert.Equal(6, change.StartLine);
            Assert.Equal(36, change.StartColumn);
            Assert.Equal(6, change.EndLine);
            Assert.Equal(36, change.EndColumn);
            Assert.Equal(8, afterInsert.Line);
            Assert.Equal(24, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PrefixHeaderIsFullyCorrect(string filename)
        {
            const string input =
@"public class Base
{
    protected virtual void OnEnable() {}
}
public class Derived : Base
{
    protected override void On$$
}";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            var onEnable = completions.Items.Single(c => c.TextEdit.NewText.Contains("OnEnable"));
            Assert.Equal(onEnable.TextEdit.StartLine, onEnable.TextEdit.EndLine);
            Assert.Equal(onEnable.TextEdit.EndColumn, onEnable.TextEdit.StartColumn);
            Assert.Equal("Enable()\n    {\n        base.OnEnable();$0\n    \\}", onEnable.TextEdit.NewText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PrefixHeaderIsPartiallyCorrect_1_Async(string filename)
        {
            const string input =
@"public class Base
{
    protected virtual void OnEnable() {}
}
public class Derived : Base
{
    protected override void ON$$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, input, host);
            var onEnable = completions.Items.Single(c => c.TextEdit.NewText.Contains("OnEnable"));
            Assert.Equal(onEnable.TextEdit.StartLine, onEnable.TextEdit.EndLine);
            Assert.Equal(2, onEnable.TextEdit.EndColumn - onEnable.TextEdit.StartColumn);
            Assert.Equal("OnEnable", onEnable.TextEdit.NewText);

            var afterInsert = await AfterInsertResponse(onEnable, host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("()\n    {\n        base.OnEnable();\n    }", change.NewText);
            Assert.Equal(6, change.StartLine);
            Assert.Equal(36, change.StartColumn);
            Assert.Equal(6, change.EndLine);
            Assert.Equal(36, change.EndColumn);
            Assert.Equal(8, afterInsert.Line);
            Assert.Equal(24, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PrefixHeaderIsPartiallyCorrect_1_Sync(string filename)
        {
            const string input =
@"public class Base
{
    protected virtual void OnEnable() {}
}
public class Derived : Base
{
    protected override void ON$$
}";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            var onEnable = completions.Items.Single(c => c.TextEdit.NewText.Contains("OnEnable"));
            Assert.Equal(onEnable.TextEdit.StartLine, onEnable.TextEdit.EndLine);
            Assert.Equal(1, onEnable.TextEdit.EndColumn - onEnable.TextEdit.StartColumn);
            Assert.Equal("nEnable()\n    {\n        base.OnEnable();$0\n    \\}", onEnable.TextEdit.NewText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PrefixHeaderIsPartiallyCorrect_2_Async(string filename)
        {
            const string input =
@"public class Base
{
    protected virtual void OnEnable() {}
}
public class Derived : Base
{
    protected override void on$$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, input, host);
            var onEnable = completions.Items.Single(c => c.TextEdit.NewText.Contains("OnEnable"));
            Assert.Equal(onEnable.TextEdit.StartLine, onEnable.TextEdit.EndLine);
            Assert.Equal(2, onEnable.TextEdit.EndColumn - onEnable.TextEdit.StartColumn);
            Assert.Equal("OnEnable", onEnable.TextEdit.NewText);

            var afterInsert = await AfterInsertResponse(onEnable, host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("()\n    {\n        base.OnEnable();\n    }", change.NewText);
            Assert.Equal(6, change.StartLine);
            Assert.Equal(36, change.StartColumn);
            Assert.Equal(6, change.EndLine);
            Assert.Equal(36, change.EndColumn);
            Assert.Equal(8, afterInsert.Line);
            Assert.Equal(24, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PrefixHeaderIsPartiallyCorrect_2_Sync(string filename)
        {
            const string input =
@"public class Base
{
    protected virtual void OnEnable() {}
}
public class Derived : Base
{
    protected override void on$$
}";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            var onEnable = completions.Items.Single(c => c.TextEdit.NewText.Contains("OnEnable"));
            Assert.Equal(onEnable.TextEdit.StartLine, onEnable.TextEdit.EndLine);
            Assert.Equal(2, onEnable.TextEdit.EndColumn - onEnable.TextEdit.StartColumn);
            Assert.Equal("OnEnable()\n    {\n        base.OnEnable();$0\n    \\}", onEnable.TextEdit.NewText);
        }

        [ConditionalTheory(typeof(WindowsOnly))]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RegexCompletionInNormalString(string filename)
        {
            const string input = @"
using System.Text.RegularExpressions;
class Foo
{
    public void M()
    {
        _ = new Regex(""$$"");
    }
}";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            var aCompletion = completions.Items.First(c => c.Label == @"\A");
            Assert.NotNull(aCompletion);
            Assert.Equal(@"\\A", aCompletion.TextEdit.NewText);
        }

        [ConditionalTheory(typeof(WindowsOnly))]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RegexCompletionInVerbatimString(string filename)
        {
            const string input = @"
using System.Text.RegularExpressions;
class Foo
{
    public void M()
    {
        _ = new Regex(@""$$"");
    }
}";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            var aCompletion = completions.Items.First(c => c.Label == @"\A");
            Assert.NotNull(aCompletion);
            Assert.Equal(@"\A", aCompletion.TextEdit.NewText);
        }

        private CompletionService GetCompletionService(OmniSharpTestHost host)
            => host.GetRequestHandler<CompletionService>(EndpointName);

        protected async Task<CompletionResponse> FindCompletionsAsync(string filename, string source, OmniSharpTestHost testHost, char? triggerChar = null, TestFile[] additionalFiles = null)
        {
            var testFile = new TestFile(filename, source);

            var files = new[] { testFile };
            if (additionalFiles is object)
            {
                files = files.Concat(additionalFiles).ToArray();
            }

            testHost.AddFilesToWorkspace(files);
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

            var requestHandler = GetCompletionService(testHost);

            return await requestHandler.Handle(request);
        }

        private async Task<CompletionResponse> FindCompletionsWithImportedAsync(string filename, string source, OmniSharpTestHost host)
        {
            var completions = await FindCompletionsAsync(filename, source, host);
            if (!completions.IsIncomplete)
            {
                return completions;
            }

            // Populating the completion list should take no more than a few ms, don't let it take too
            // long
            CancellationTokenSource cts = new CancellationTokenSource(millisecondsDelay: ImportCompletionTimeout);
            await Task.Run(async () =>
            {
                while (completions.IsIncomplete)
                {
                    completions = await FindCompletionsAsync(filename, source, host);
                    cts.Token.ThrowIfCancellationRequested();
                }
            }, cts.Token);

            Assert.False(completions.IsIncomplete);
            return completions;
        }

        protected async Task<CompletionResolveResponse> ResolveCompletionAsync(CompletionItem completionItem, OmniSharpTestHost testHost)
            => await GetCompletionService(testHost).Handle(new CompletionResolveRequest { Item = completionItem });

        private OmniSharpTestHost GetImportCompletionHost()
        {
            var testHost = CreateOmniSharpHost(configurationData: new[] { new KeyValuePair<string, string>("RoslynExtensionsOptions:EnableImportCompletion", "true") });
            testHost.AddFilesToWorkspace();
            return testHost;
        }

        private OmniSharpTestHost GetAsyncCompletionAndImportCompletionHost()
        {
            var testHost = CreateOmniSharpHost(configurationData: new[] {
                new KeyValuePair<string, string>("RoslynExtensionsOptions:EnableImportCompletion", "true"),
                new KeyValuePair<string, string>("RoslynExtensionsOptions:EnableAsyncCompletion", "true"),
            });
            testHost.AddFilesToWorkspace();
            return testHost;
        }

        private Task<CompletionAfterInsertResponse> AfterInsertResponse(CompletionItem completionItem, OmniSharpTestHost testHost)
        {
            return GetCompletionService(testHost).Handle(new CompletionAfterInsertRequest
            {
                Item = completionItem
            });
        }

        private static string NormalizeNewlines(string str)
            => str.Replace("\r\n", Environment.NewLine);

        private static void VerifySortOrders(IReadOnlyList<CompletionItem> items)
        {
            Assert.All(items, c =>
            {
                Assert.True(c.SortText.StartsWith("0") || c.SortText.StartsWith("1"));
            });
        }
    }

    internal static class CompletionResponseExtensions
    {
        public static bool IsSuggestionMode(this CompletionItem item) => !item.CommitCharacters?.Contains(' ') ?? true;
    }
}
