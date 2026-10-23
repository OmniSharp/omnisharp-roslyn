using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.v1.InlayHints;
using OmniSharp.Models.V2;
using OmniSharp.Options;
using Roslyn.Test.Utilities;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests;

public class InlayHintsParameterFacts : InlayHintsTestBase
{
    public InlayHintsParameterFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
        : base(output, sharedOmniSharpHostFixture)
    {
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsForLiteralParameters(string fileName)
    {
        var code = @"
{|ihRegion:M(1)|};
void M(int i) {}
";

        var options = InlayHintsOptions.AllOn with { ForLiteralParameters = false };

        {
            using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));
            var response = await GetInlayHints(fileName, code, testHost);
            Assert.Empty(response.InlayHints);
        }
        {
            using var testHost = CreateOmniSharpHost(configurationData:
                InlayHintsOptionsToKvp(options with { ForLiteralParameters = true }));
            var response = await GetInlayHints(fileName, code, testHost);
            AssertEx.Equal(new[]
                {
                new InlayHint { Position =  new Point { Line = 1, Column = 2 }, Label = "i: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 1, StartColumn = 2, EndLine = 1, EndColumn = 2, NewText = "i: " } } }
            },
                response.InlayHints);
        }
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsForIndexerParameters(string fileName)
    {
        var code = @"
var c = new C();
int i = 1;
{|ihRegion:c[i] = c[i]|};

class C
{
    public int this[int test] { get => throw null; set => throw null; }
}
";

        var options = InlayHintsOptions.AllOn with { ForIndexerParameters = false };

        {
            using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));
            var response = await GetInlayHints(fileName, code, testHost);
            Assert.Empty(response.InlayHints);
        }
        {
            using var testHost = CreateOmniSharpHost(configurationData:
                InlayHintsOptionsToKvp(options with { ForIndexerParameters = true }));
            var response = await GetInlayHints(fileName, code, testHost);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position =  new Point { Line = 3, Column = 2 }, Label = "test: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 3, StartColumn = 2, EndLine = 3, EndColumn = 2, NewText = "test: " } } },
                    new InlayHint { Position =  new Point { Line = 3, Column = 9 }, Label = "test: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 3, StartColumn = 9, EndLine = 3, EndColumn = 9, NewText = "test: " } } }
                },
                response.InlayHints);
        }
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsForObjectCreationParameters(string fileName)
    {
        var code = @"
int i = 1;
{|ihRegion:M(new C())|};

void M(C c) {}

class C
{
}
";

        var options = InlayHintsOptions.AllOn with { ForObjectCreationParameters = false };

        {
            using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));
            var response = await GetInlayHints(fileName, code, testHost);
            Assert.Empty(response.InlayHints);
        }
        {
            using var testHost = CreateOmniSharpHost(configurationData:
                InlayHintsOptionsToKvp(options with { ForObjectCreationParameters = true }));
            var response = await GetInlayHints(fileName, code, testHost);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position =  new Point { Line = 2, Column = 2 }, Label = "c: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 2, StartColumn = 2, EndLine = 2, EndColumn = 2, NewText = "c: " } } }
                },
                response.InlayHints);
        }
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsForOtherParameters(string fileName)
    {
        var code = @"
int i = 1;
{|ihRegion:M(i)|};

void M(int test) {}
";

        var options = InlayHintsOptions.AllOn with { ForOtherParameters = false };

        {

            using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));
            var response = await GetInlayHints(fileName, code, testHost);
            Assert.Empty(response.InlayHints);
        }
        {

            using var testHost = CreateOmniSharpHost(configurationData:
                InlayHintsOptionsToKvp(options with { ForOtherParameters = true }));
            var response = await GetInlayHints(fileName, code, testHost);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position =  new Point { Line = 2, Column = 2 }, Label = "test: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 2, StartColumn = 2, EndLine = 2, EndColumn = 2, NewText = "test: " } } }
                },
                response.InlayHints);
        }
    }
}
