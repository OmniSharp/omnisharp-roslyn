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
    public class CompletionPrefixAndRegexFacts : CompletionTestBase
    {
        public CompletionPrefixAndRegexFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
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
            Assert.Contains(completions.Items, completion => completion.Preselect == true);
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

        [ConditionalTheory(typeof(WindowsOnly), typeof(DesktopRuntimeOnly))]
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

        [ConditionalTheory(typeof(WindowsOnly), typeof(DesktopRuntimeOnly))]
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

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task NoTriggerCharactersWhenInSuggestionMode(string filename)
        {
            const string input = @"
using System;

class Program
{
    public static void Main()
    {
        var fn = (DateTime $$
    }
}";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            foreach (var item in completions.Items)
            {
                Assert.Null(item.CommitCharacters);
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task TestOverrideWithTrailingWhitespacePrior(string filename)
        {
            string input = $$"""
                namespace N
                {
                    internal class C
                    {{{'\t'}}
                // The trailing tabs on the previous line and the next line are integral to this bug
                {{'\t'}}
                        override $$
                        public C()
                        {
                        }
                    }
                }
                """;

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);

            foreach (var item in completions.Items)
            {
                Assert.Single(item.AdditionalTextEdits);
                Assert.Equal("\n        // The trailing tabs on the previous line and the next line are integral to this bug\n\n", NormalizeNewlines(item.AdditionalTextEdits[0].NewText));
                Assert.StartsWith("        public override ", item.TextEdit.NewText);
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReplacesUpUntilCursorInMiddleOfWord(string filename)
        {
            const string input = @"public class C1 {}
pub$$class";

            var completions = await FindCompletionsAsync(filename, input, SharedOmniSharpTestHost);
            Assert.All(completions.Items, (completion) =>
            {
                Assert.Equal(0, completion.TextEdit.StartColumn);
                Assert.Equal(1, completion.TextEdit.StartLine);
                Assert.Equal(3, completion.TextEdit.EndColumn);
                Assert.Equal(1, completion.TextEdit.EndLine);
            });
        }

        [Theory]
        [InlineData("dummy.cs", true)]
        [InlineData("dummy.cs", false)]
        [InlineData("dummy.csx", true)]
        [InlineData("dummy.csx", false)]
        public async Task SoftSelectionWhenFilterTextIsEmpty(string filename, bool useAsyncCompletion)
        {
            const string input = @"
using System;
using System.Text;
public class A
{
    public void M(string someText)
    {
        var x = new StringBuilder();
        x.Append($$
    }
}";

            using var host = useAsyncCompletion ? GetAsyncCompletionAndImportCompletionHost() : GetImportCompletionHost();

            var completions = await FindCompletionsAsync(filename, input, host, '(');
            var someTextItem = completions.Items.First(item => item.Label == "someText");

            Assert.Null(someTextItem.CommitCharacters);
            Assert.False(someTextItem.Preselect);
        }
    }
}
