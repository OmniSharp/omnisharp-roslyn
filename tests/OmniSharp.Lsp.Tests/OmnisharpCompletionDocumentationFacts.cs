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
    public class OmnisharpCompletionDocumentationFacts : OmnisharpCompletionTestBase
    {
        public OmnisharpCompletionDocumentationFacts(ITestOutputHelper output) : base(output)
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

            var completions = await FindCompletionsAsync(filename, source);
            Assert.Contains(completions.Items, c => c.Label == "MyClass1");
            Assert.All(completions.Items, c => Assert.False(c.Preselect));
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

            var completions = await FindCompletionsAsync(filename, source);
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
                         completions.Items.Select(c => c.TextEdit.TextEdit.NewText));
            Assert.All(completions.Items, c => Assert.Equal(c.TextEdit.TextEdit.NewText.Contains("$0"), c.InsertTextFormat == InsertTextFormat.Snippet));

            Assert.All(completions.Items, c =>
            {
                if (c.InsertTextFormat == InsertTextFormat.Snippet)
                {
                    Assert.Equal(35, c.TextEdit.TextEdit.Range.Start.Character);
                    Assert.Contains("0", c.TextEdit.TextEdit.NewText);
                    Assert.StartsWith("<", c.FilterText);
                }
                else
                {
                    Assert.Equal(36, c.TextEdit.TextEdit.Range.Start.Character);
                    Assert.DoesNotContain("0", c.TextEdit.TextEdit.NewText);
                    Assert.Null(c.FilterText);
                }
            });
        }

    }
}
