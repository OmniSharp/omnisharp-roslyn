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
    public class OmnisharpCompletionPrefixAndRegexFacts : OmnisharpCompletionTestBase
    {
        public OmnisharpCompletionPrefixAndRegexFacts(ITestOutputHelper output) : base(output)
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

            var completions = await FindCompletionsAsync(filename, input, triggerChar: ' ');
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

            var completions = await FindCompletionsAsync(filename, input, triggerChar: ' ');
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

            var completions = await FindCompletionsAsync(filename, input, triggerChar: ' ');
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

            OmniSharpTestHost.Workspace.AddProject(projectInfo);

            const string input = @"
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""$$";

            var completions = await FindCompletionsAsync("dummy.cs", input);
            Assert.Single(completions.Items);
            Assert.Equal("AssemblyNameVal", completions.First().Label);
            Assert.Equal("AssemblyNameVal", completions.First().TextEdit.TextEdit.NewText);
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

            OmniSharpTestHost.Workspace.AddProject(projectInfo);

            var miscFile = "class Foo {}";
            var miscFileLoader = TextLoader.From(TextAndVersion.Create(SourceText.From(miscFile), VersionStamp.Create()));
            OmniSharpTestHost.Workspace.TryAddMiscellaneousDocument("dummy.cs", miscFileLoader, LanguageNames.CSharp);

            const string input = @"
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""$$";

            var completions = await FindCompletionsAsync("dummy.cs", input);
            Assert.Single(completions.Items);
            Assert.Equal("AssemblyNameVal", completions.First().Label);
            Assert.Equal("AssemblyNameVal", completions.First().TextEdit.TextEdit.NewText);
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

            var completions = await FindCompletionsAsync(filename, input);
            var onEnable = completions.Items.Single(c => c.TextEdit.TextEdit.NewText.Contains("OnEnable"));
            Assert.Equal(onEnable.TextEdit.TextEdit.Range.Start.Line, onEnable.TextEdit.TextEdit.Range.End.Line);
            Assert.Equal(onEnable.TextEdit.TextEdit.Range.End.Character, onEnable.TextEdit.TextEdit.Range.Start.Character);
            Assert.Equal("Enable()\n    {\n        base.OnEnable();$0\n    \\}", onEnable.TextEdit.TextEdit.NewText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PrefixHeaderIsPartiallyCorrect_1(string filename)
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

            var completions = await FindCompletionsAsync(filename, input);
            var onEnable = completions.Items.Single(c => c.TextEdit.TextEdit.NewText.Contains("OnEnable"));
            Assert.Equal(onEnable.TextEdit.TextEdit.Range.Start.Line, onEnable.TextEdit.TextEdit.Range.End.Line);
            Assert.Equal(1, onEnable.TextEdit.TextEdit.Range.End.Character - onEnable.TextEdit.TextEdit.Range.Start.Character);
            Assert.Equal("nEnable()\n    {\n        base.OnEnable();$0\n    \\}", onEnable.TextEdit.TextEdit.NewText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PrefixHeaderIsPartiallyCorrect_2(string filename)
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

            var completions = await FindCompletionsAsync(filename, input);
            var onEnable = completions.Items.Single(c => c.TextEdit.TextEdit.NewText.Contains("OnEnable"));
            Assert.Equal(onEnable.TextEdit.TextEdit.Range.Start.Line, onEnable.TextEdit.TextEdit.Range.End.Line);
            Assert.Equal(2, onEnable.TextEdit.TextEdit.Range.End.Character - onEnable.TextEdit.TextEdit.Range.Start.Character);
            Assert.Equal("OnEnable()\n    {\n        base.OnEnable();$0\n    \\}", onEnable.TextEdit.TextEdit.NewText);
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

            var completions = await FindCompletionsAsync(filename, input);
            var aCompletion = completions.Items.First(c => c.Label == @"\A");
            Assert.NotNull(aCompletion);
            Assert.Equal(@"\\A", aCompletion.TextEdit.TextEdit.NewText);
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

            var completions = await FindCompletionsAsync(filename, input);
            var aCompletion = completions.Items.First(c => c.Label == @"\A");
            Assert.NotNull(aCompletion);
            Assert.Equal(@"\A", aCompletion.TextEdit.TextEdit.NewText);
        }

    }
}
