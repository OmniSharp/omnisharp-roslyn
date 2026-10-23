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
    public class CompletionDocumentationFacts : CompletionTestBase
    {
        public CompletionDocumentationFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
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
                                 "b",
                                 "c",
                                 "code",
                                 "em",
                                 "i",
                                 "<inheritdoc$0/>",
                                 "<list type=\"$0\"",
                                 "para",
                                 "<see cref=\"$0\"/>",
                                 "<seealso cref=\"$0\"/>",
                                 "strong",
                                 "tt"
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
    }
}
