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

public class InlayHintsGeneralFacts : InlayHintsTestBase
{
    public InlayHintsGeneralFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
        : base(output, sharedOmniSharpHostFixture)
    {
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsRetrievedForTopLevelStatements(string fileName)
    {
        var code = @"
{|ihRegion:var testA = new C();
var testB = new C();
M(testA, testB)|};

void M(C param1, C paramB) { }

class C { }
";

        using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(InlayHintsOptions.AllOn));
        var response = await GetInlayHints(fileName, code, testHost);
        AssertEx.Equal(new[]
            {
                new InlayHint { Position = new Point { Line = 3, Column = 2 }, Label = "param1: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 3, StartColumn = 2, EndLine = 3, EndColumn = 2, NewText = "param1: " } } },
                new InlayHint { Position = new Point { Line = 3, Column = 9 }, Label = "paramB: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 3, StartColumn = 9, EndLine = 3, EndColumn = 9, NewText = "paramB: " } } },
                new InlayHint { Position = new Point { Line = 1, Column = 4 }, Label = "C ", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 1, StartColumn = 0, EndLine = 1, EndColumn = 3, NewText = "C" } } },
                new InlayHint { Position = new Point { Line = 2, Column = 4 }, Label = "C ", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 2, StartColumn = 0, EndLine = 2, EndColumn = 3, NewText = "C" } } },
            },
            response.InlayHints);

        var param1 = await ResolveInlayHint(response.InlayHints[0], testHost);
        AssertEx.AssertEqualToleratingWhitespaceDifferences(@"
```csharp
(parameter) C param1
```", param1.Tooltip);

        var paramB = await ResolveInlayHint(response.InlayHints[1], testHost);
        AssertEx.AssertEqualToleratingWhitespaceDifferences(@"
```csharp
(parameter) C paramB
```", paramB.Tooltip);

        var c1 = await ResolveInlayHint(response.InlayHints[2], testHost);
        AssertEx.AssertEqualToleratingWhitespaceDifferences(@"
```csharp
class C
```", c1.Tooltip);

        var c2 = await ResolveInlayHint(response.InlayHints[3], testHost);
        AssertEx.AssertEqualToleratingWhitespaceDifferences(@"
```csharp
class C
```", c2.Tooltip);
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsRetrievedForOnlyTypes(string fileName)
    {
        var code = @"
{|ihRegion:var testA = 1;
var testB = 2;
M(testA, testB)|};

void M(int param1, int paramB) { }
";

        var options = InlayHintsOptions.AllOn with { EnableForParameters = false };
        using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));

        var response = await GetInlayHints(fileName, code, testHost);
        AssertEx.Equal(new[]
            {
                new InlayHint { Position = new Point { Line = 1, Column = 4 }, Label = "int ", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 1, StartColumn = 0, EndLine = 1, EndColumn = 3, NewText = "int" } } },
                new InlayHint { Position = new Point { Line = 2, Column = 4 }, Label = "int ", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 2, StartColumn = 0, EndLine = 2, EndColumn = 3, NewText = "int" } } },
            },
            response.InlayHints);
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsRetrievedForOnlyParameters(string fileName)
    {
        var code = @"
{|ihRegion:var testA = 1;
var testB = 2;
M(testA, testB)|};

void M(int param1, int paramB) { }
";

        var options = InlayHintsOptions.AllOn with { EnableForTypes = false };
        using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));

        var response = await GetInlayHints(fileName, code, testHost);
        AssertEx.Equal(new[]
            {
                new InlayHint { Position = new Point { Line = 3, Column = 2 }, Label = "param1: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 3, StartColumn = 2, EndLine = 3, EndColumn = 2, NewText = "param1: " } } },
                new InlayHint { Position = new Point { Line = 3, Column = 9 }, Label = "paramB: ", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 3, StartColumn = 9, EndLine = 3, EndColumn = 9, NewText = "paramB: " } } },
            },
            response.InlayHints);
    }
}
