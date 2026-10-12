using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Options;
using Roslyn.Test.Utilities;
using TestUtility;
using Xunit;

namespace OmniSharp.Lsp.Tests;

public class OmnisharpInlayHintGeneralFacts : OmnisharpInlayHintTestBase
{
    public OmnisharpInlayHintGeneralFacts(ITestOutputHelper output) : base(output)
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

        await SetInlayHintOptionsAsync(InlayHintsOptions.AllOn);

        var response = await GetInlayHints(fileName, code);
        InlayHint[] inlayHints = response.ToArray();

        AssertEx.Equal(new[]
            {
                new InlayHint { Position = new Position { Line = 3, Character = 2 }, Label = "param1:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 3, Character = 2 }, End = new Position() { Line = 3, Character = 2 } }, NewText = "param1: " } }, PaddingLeft = false, PaddingRight = true },
                new InlayHint { Position = new Position { Line = 3, Character = 9 }, Label = "paramB:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 3, Character = 9 }, End = new Position() { Line = 3, Character = 9 } }, NewText = "paramB: " } }, PaddingLeft = false, PaddingRight = true },
                new InlayHint { Position = new Position { Line = 1, Character = 4 }, Label = "C", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 1, Character = 0 }, End = new Position() { Line = 1, Character = 3 } }, NewText = "C" } }, PaddingLeft = false, PaddingRight = true },
                new InlayHint { Position = new Position { Line = 2, Character = 4 }, Label = "C", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 2, Character = 0 }, End = new Position() { Line = 2, Character = 3 } }, NewText = "C" } }, PaddingLeft = false, PaddingRight = true },
            },
            inlayHints,
            ignoreDataComparer);

        var param1 = await ResolveInlayHint(inlayHints[0]);
        AssertEx.AssertEqualToleratingWhitespaceDifferences(@"
```csharp
(parameter) C param1
```", param1.Tooltip.MarkupContent.Value);

        var paramB = await ResolveInlayHint(inlayHints[1]);
        AssertEx.AssertEqualToleratingWhitespaceDifferences(@"
```csharp
(parameter) C paramB
```", paramB.Tooltip.MarkupContent.Value);

        var c1 = await ResolveInlayHint(inlayHints[2]);
        AssertEx.AssertEqualToleratingWhitespaceDifferences(@"
```csharp
class C
```", c1.Tooltip.MarkupContent.Value);

        var c2 = await ResolveInlayHint(inlayHints[3]);
        AssertEx.AssertEqualToleratingWhitespaceDifferences(@"
```csharp
class C
```", c2.Tooltip.MarkupContent.Value);
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
        await SetInlayHintOptionsAsync(options);

        var response = await GetInlayHints(fileName, code);
        AssertEx.Equal(new[]
            {
                new InlayHint { Position = new Position { Line = 1, Character = 4 }, Label = "int", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 1, Character = 0 }, End = new Position() { Line = 1, Character = 3 } }, NewText = "int" } }, PaddingLeft = false, PaddingRight = true },
                new InlayHint { Position = new Position { Line = 2, Character = 4 }, Label = "int", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 2, Character = 0 }, End = new Position() { Line = 2, Character = 3 } }, NewText = "int" } }, PaddingLeft = false, PaddingRight = true },
            },
            response,
            ignoreDataComparer);
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
        await SetInlayHintOptionsAsync(options);

        var response = await GetInlayHints(fileName, code);
        AssertEx.Equal(new[]
            {
                new InlayHint { Position = new Position { Line = 3, Character = 2 }, Label = "param1:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 3, Character = 2 }, End = new Position() { Line = 3, Character = 2 } }, NewText = "param1: " } }, PaddingLeft = false, PaddingRight = true },
                new InlayHint { Position = new Position { Line = 3, Character = 9 }, Label = "paramB:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 3, Character = 9 }, End = new Position() { Line = 3, Character = 9 } }, NewText = "paramB: " } }, PaddingLeft = false, PaddingRight = true },
            },
            response,
            ignoreDataComparer);
    }
}
