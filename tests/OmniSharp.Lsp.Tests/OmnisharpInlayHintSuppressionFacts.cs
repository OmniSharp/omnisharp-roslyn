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

public class OmnisharpInlayHintSuppressionFacts : OmnisharpInlayHintTestBase
{
    public OmnisharpInlayHintSuppressionFacts(ITestOutputHelper output) : base(output)
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
            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            Assert.Empty(response);
        }

        {
            await SetInlayHintOptionsAsync(options with { SuppressForParametersThatDifferOnlyBySuffix = false });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Position { Line = 1, Character = 2 }, Label = "test1:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 1, Character = 2 }, End = new Position() { Line = 1, Character = 2 } }, NewText = "test1: " } }, PaddingLeft = false, PaddingRight = true },
                    new InlayHint { Position = new Position { Line = 1, Character = 5 }, Label = "test2:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 1, Character = 5 }, End = new Position() { Line = 1, Character = 5 } }, NewText = "test2: " } }, PaddingLeft = false, PaddingRight = true }
                },
                response,
                ignoreDataComparer);
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
            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            Assert.Empty(response);
        }

        {
            await SetInlayHintOptionsAsync(options with { SuppressForParametersThatMatchMethodIntent = false });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Position { Line = 1, Character = 18 }, Label = "enabled:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 1, Character = 18 }, End = new Position() { Line = 1, Character = 18 } }, NewText = "enabled: " } }, PaddingLeft = false, PaddingRight = true }
                },
                response,
                ignoreDataComparer);
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
            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            Assert.Empty(response);
        }

        {
            await SetInlayHintOptionsAsync(options with { SuppressForParametersThatMatchArgumentName = false });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Position { Line = 2, Character = 4 }, Label = "i:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 2, Character = 4 }, End = new Position() { Line = 2, Character = 4 } }, NewText = "i: " } }, PaddingLeft = false, PaddingRight = true }
                },
                response,
                ignoreDataComparer);
        }
    }
}
