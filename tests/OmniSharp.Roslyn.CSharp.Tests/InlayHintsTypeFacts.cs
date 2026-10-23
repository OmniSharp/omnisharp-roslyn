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

public class InlayHintsTypeFacts : InlayHintsTestBase
{
    public InlayHintsTypeFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
        : base(output, sharedOmniSharpHostFixture)
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
            using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));

            var response = await GetInlayHints(fileName, code, testHost);
            Assert.Empty(response.InlayHints);
        }

        {
            using var testHost = CreateOmniSharpHost(configurationData:
                InlayHintsOptionsToKvp(options with { ForImplicitVariableTypes = true }));
            var response = await GetInlayHints(fileName, code, testHost);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Point { Line = 1, Column = 4 }, Label = "int ", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 1, StartColumn = 0, EndLine = 1, EndColumn = 3, NewText = "int" } } },
                },
                response.InlayHints);
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
            using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));
            var response = await GetInlayHints(fileName, code, testHost);
            Assert.Empty(response.InlayHints);
        }
        {
            using var testHost = CreateOmniSharpHost(configurationData:
                InlayHintsOptionsToKvp(options with { ForLambdaParameterTypes = true }));
            var response = await GetInlayHints(fileName, code, testHost);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position = new Point { Line = 2, Column = 34 }, Label = "int ", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new  LinePositionSpanTextChange { StartLine = 2, StartColumn = 34, EndLine = 2, EndColumn = 34, NewText = "int " } } },
                    new InlayHint { Position = new Point { Line = 2, Column = 37 }, Label = "string ", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new  LinePositionSpanTextChange { StartLine = 2, StartColumn = 37, EndLine = 2, EndColumn = 37, NewText = "string " } } }
                },
                response.InlayHints);
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
            using var testHost = CreateOmniSharpHost(configurationData: InlayHintsOptionsToKvp(options));
            var response = await GetInlayHints(fileName, code, testHost);
            Assert.Empty(response.InlayHints);
        }
        {
            using var testHost = CreateOmniSharpHost(configurationData:
                InlayHintsOptionsToKvp(options with { ForImplicitObjectCreation = true }));
            var response = await GetInlayHints(fileName, code, testHost);
            AssertEx.Equal(new[]
                {
                    new InlayHint { Position =  new Point { Line = 1, Column = 14 }, Label = " string", Kind = InlayHintKind.Type, Tooltip = null, TextEdits = new[] { new LinePositionSpanTextChange { StartLine = 1, StartColumn = 14, EndLine = 1, EndColumn = 14, NewText = " string" } } }
                },
                response.InlayHints);
        }
    }
}
