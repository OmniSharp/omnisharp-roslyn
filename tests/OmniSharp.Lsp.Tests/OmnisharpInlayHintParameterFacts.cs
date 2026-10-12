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

public class OmnisharpInlayHintParameterFacts : OmnisharpInlayHintTestBase
{
    public OmnisharpInlayHintParameterFacts(ITestOutputHelper output) : base(output)
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
            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            Assert.Empty(response);
        }

        {
            await SetInlayHintOptionsAsync(options with { ForLiteralParameters = true });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position =  new Position { Line = 1, Character = 2 }, Label = "i:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 1, Character = 2 }, End = new Position() { Line = 1, Character = 2 } }, NewText = "i: " } }, PaddingLeft = false, PaddingRight = true }
                },
                response,
                ignoreDataComparer);
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
            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            Assert.Empty(response);
        }

        {
            await SetInlayHintOptionsAsync(options with { ForIndexerParameters = true });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position =  new Position { Line = 3, Character = 2 }, Label = "test:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 3, Character = 2 }, End = new Position() { Line = 3, Character = 2 } }, NewText = "test: " } }, PaddingLeft = false, PaddingRight = true },
                    new InlayHint { Position =  new Position { Line = 3, Character = 9 }, Label = "test:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 3, Character = 9 }, End = new Position() { Line = 3, Character = 9 } }, NewText = "test: " } }, PaddingLeft = false, PaddingRight = true }
                },
                response,
                ignoreDataComparer);
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
            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            Assert.Empty(response);
        }

        {
            await SetInlayHintOptionsAsync(options with { ForObjectCreationParameters = true });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position =  new Position { Line = 2, Character = 2 }, Label = "c:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 2, Character = 2 }, End = new Position() { Line = 2, Character = 2 } }, NewText = "c: " } }, PaddingLeft = false, PaddingRight = true }
                },
                response,
                ignoreDataComparer);
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

            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            Assert.Empty(response);
        }

        {
            await SetInlayHintOptionsAsync(options with { ForOtherParameters = true });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position =  new Position { Line = 2, Character = 2 }, Label = "test:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 2, Character = 2 }, End = new Position() { Line = 2, Character = 2 } }, NewText = "test: " } }, PaddingLeft = false, PaddingRight = true }
                },
                response,
                ignoreDataComparer);
        }
    }
}
