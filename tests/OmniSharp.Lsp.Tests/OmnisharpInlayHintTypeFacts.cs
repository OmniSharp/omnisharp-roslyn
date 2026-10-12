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

public class OmnisharpInlayHintTypeFacts : OmnisharpInlayHintTestBase
{
    public OmnisharpInlayHintTypeFacts(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsForVarTypes(string fileName)
    {
        var code = @"
{|ihRegion:var x = 1|};
";

        var options = InlayHintsOptions.AllOn with { ForImplicitVariableTypes = false };

        {
            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            Assert.Empty(response);
        }

        {
            await SetInlayHintOptionsAsync(options with { ForImplicitVariableTypes = true });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Position { Line = 1, Character = 4 }, Label = "int", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 1, Character = 0 }, End = new Position() { Line = 1, Character = 3 } }, NewText = "int" } }, PaddingLeft = false, PaddingRight = true },
                },
                response,
                ignoreDataComparer);
        }
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsForLambdaParameterTypeNearFunctionParameterName(string fileName)
    {
        var code = @"
using System;
void fun(Func<int, bool> lambda) {}
{|ihRegion:fun(b => true);|}
";

        var options = InlayHintsOptions.AllOn with { ForLambdaParameterTypes = false };

        {
            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Position { Line = 3, Character = 4 }, Label = "lambda:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 3, Character = 4 }, End = new Position() { Line = 3, Character = 4 } }, NewText = "lambda: " } }, PaddingLeft = false, PaddingRight = true },
                },
                response,
                ignoreDataComparer);
        }

        {
            await SetInlayHintOptionsAsync(options with { ForLambdaParameterTypes = true });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Position { Line = 3, Character = 4 }, Label = "lambda:", Kind = InlayHintKind.Parameter, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 3, Character = 4 }, End = new Position() { Line = 3, Character = 4 } }, NewText = "lambda: " } }, PaddingLeft = false, PaddingRight = true },
                    new InlayHint { Position = new Position { Line = 3, Character = 4 }, Label = "int", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = null, PaddingLeft = false, PaddingRight = true },
                },
                response,
                ignoreDataComparer);
        }
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsForLambdaParameterTypes(string fileName)
    {
        var code = @"
using System;
{|ihRegion:Func<int, string, bool> lambda = (a, b) => true;|}
";

        var options = InlayHintsOptions.AllOn with { ForLambdaParameterTypes = false };

        {
            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            Assert.Empty(response);
        }

        {
            await SetInlayHintOptionsAsync(options with { ForLambdaParameterTypes = true });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Position { Line = 2, Character = 34 }, Label = "int", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 2, Character = 34 }, End = new Position() { Line = 2, Character = 34 } }, NewText = "int " } }, PaddingLeft = false, PaddingRight = true },
                    new InlayHint { Position = new Position { Line = 2, Character = 37 }, Label = "string", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 2, Character = 37 }, End = new Position() { Line = 2, Character = 37 } }, NewText = "string " } }, PaddingLeft = false, PaddingRight = true }
                },
                response,
                ignoreDataComparer);
        }
    }

    [Theory]
    [InlineData("dummy.cs")]
    [InlineData("dummy.csx")]
    public async Task InlayHintsForImplicitObjectCreation(string fileName)
    {
        var code = @"
{|ihRegion:string x = new()|};
";

        var options = InlayHintsOptions.AllOn with { ForImplicitObjectCreation = false };

        {
            await SetInlayHintOptionsAsync(options);
            var response = await GetInlayHints(fileName, code);
            Assert.Empty(response);
        }

        {
            await SetInlayHintOptionsAsync(options with { ForImplicitObjectCreation = true });
            var response = await GetInlayHints(fileName, code);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position =  new Position { Line = 1, Character = 14 }, Label = "string", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new TextEdit { Range = new Range() { Start = new Position() { Line = 1, Character = 14 }, End = new Position() { Line = 1, Character = 14 } }, NewText = " string" } }, PaddingLeft = true, PaddingRight = false }
                },
                response,
                ignoreDataComparer);
        }
    }
}
