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
    public class OmnisharpCompletionCommitBehaviorFacts : OmnisharpCompletionTestBase
    {
        public OmnisharpCompletionCommitBehaviorFacts(ITestOutputHelper output) : base(output)
        {
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

            var completions = await FindCompletionsAsync(filename, source);

            Assert.NotEmpty(completions.Items);
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

            var completions = await FindCompletionsAsync(filename, source);

            Assert.NotEmpty(completions.Items);
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

            var completions = await FindCompletionsAsync(filename, source);

            Assert.NotEmpty(completions.Items);
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

            var completions = await FindCompletionsAsync(filename, source);

            Assert.NotEmpty(completions.Items);
            Assert.True(completions.Items.All(c => !c.IsSuggestionMode()));
        }

    }
}
