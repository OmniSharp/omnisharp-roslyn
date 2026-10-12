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
    public class CompletionOverrideFacts : CompletionTestBase
    {
        public CompletionOverrideFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        [Fact]
        public async Task ShouldNotGetAdditionalTextEditsFromOverrideCompletion()
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

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");
                var completions = await FindCompletionsAsync(fileName, source, host);
                Assert.Equal(
                    new[]
                    {
                        "Equals(object? obj)",
                        "GetHashCode()",
                        "Test(string text)",
                        "Test(string text, string moreText)",
                        "ToString()"
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
    }
}
