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

public class InlayHintsSuppressionFacts : InlayHintsTestBase
{
    public InlayHintsSuppressionFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
        : base(output, sharedOmniSharpHostFixture)
    {
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsSuppressForParametersThatDifferOnlyBySuffix(string fileName)
    {
        var code = @"
{|ihRegion:M(1, 2)|};

void M(int test1, int test2) {}
";

        var options = InlayHintsOptions.AllOn;

        {
            using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));
            var response = await GetInlayHints(fileName, code, testHost);
            Assert.Empty(response.InlayHints);
        }
        {

            using var testHost = CreateOmniSharpHost(configurationData:
                InlayHintsOptionsToKvp(options with { SuppressForParametersThatDifferOnlyBySuffix = false }));
            var response = await GetInlayHints(fileName, code, testHost);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Point { Line = 1, Column = 2 }, Label = "test1: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 1, StartColumn = 2, EndLine = 1, EndColumn = 2, NewText = "test1: " } } },
                    new InlayHint { Position = new Point { Line = 1, Column = 5 }, Label = "test2: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 1, StartColumn = 5, EndLine = 1, EndColumn = 5, NewText = "test2: " } } }
                },
                response.InlayHints);
        }
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsSuppressForParametersThatMatchMethodIntent(string fileName)
    {
        var code = @"
{|ihRegion:C.EnableSomething(true)|};

class C
{
    public static void EnableSomething(bool enabled) {}
}
";

        var options = InlayHintsOptions.AllOn;

        {
            using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));
            var response = await GetInlayHints(fileName, code, testHost);
            Assert.Empty(response.InlayHints);
        }
        {
            using var testHost = CreateOmniSharpHost(configurationData:
                InlayHintsOptionsToKvp(options with { SuppressForParametersThatMatchMethodIntent = false }));
            var response = await GetInlayHints(fileName, code, testHost);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Point { Line = 1, Column = 18 }, Label = "enabled: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 1, StartColumn = 18, EndLine = 1, EndColumn = 18, NewText = "enabled: " } } }
                },
                response.InlayHints);
        }
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsSuppressForParametersThatMatchArgumentName(string fileName)
    {
        var code = @"
int i = 0;
{|ihRegion:C.M(i)|};

class C
{
    public static void M(int i) {}
}
";

        var options = InlayHintsOptions.AllOn;

        {
            using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));
            var response = await GetInlayHints(fileName, code, testHost);
            Assert.Empty(response.InlayHints);
        }
        {

            using var testHost = CreateOmniSharpHost(configurationData:
                InlayHintsOptionsToKvp(options with { SuppressForParametersThatMatchArgumentName = false }));
            var response = await GetInlayHints(fileName, code, testHost);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Point { Line = 2, Column = 4 }, Label = "i: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 2, StartColumn = 4, EndLine = 2, EndColumn = 4, NewText = "i: " } } }
                },
                response.InlayHints);
        }
    }
}
