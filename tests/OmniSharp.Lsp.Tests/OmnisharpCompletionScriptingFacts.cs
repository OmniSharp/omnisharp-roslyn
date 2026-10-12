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
    public partial class OmnisharpCompletionScriptingFacts : OmnisharpCompletionTestBase
    {
        public OmnisharpCompletionScriptingFacts(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task HostObjectCompletionInScripts()
        {
            const string source =
                "Prin$$";

            var completions = await FindCompletionsAsync("dummy.csx", source);
            Assert.Contains(completions.Items, c => c.Label == "Print");
            Assert.Contains(completions.Items, c => c.Label == "PrintOptions");
        }

    }

    public partial class OmnisharpCompletionScriptingFacts
    {
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

            var completions = await FindCompletionsAsync("dummy.csx", source);
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

            var completions = await FindCompletionsAsync("dummy.csx", source);
            Assert.Contains(completions.Items, c => c.Label == "myValue");
            Assert.All(completions.Items, c => Assert.False(c.Preselect));
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

            var completions = await FindCompletionsAsync("dummy.csx", source);
            Assert.Contains(completions.Items, c => c.Label == "PositionX");
            Assert.Contains(completions.Items, c => c.Label == "PositionY");
            Assert.All(completions.Items, c => Assert.False(c.Preselect));
        }

    }
}
