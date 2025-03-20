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
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests;

public class InlayHintsFacts : AbstractTestFixture
{
    public InlayHintsFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture) : base(output, sharedOmniSharpHostFixture)
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

    private static Task<InlayHintResponse> GetInlayHints(string fileName, string code, OmniSharpTestHost testHost)
    {
        var testFile = new TestFile(fileName, code);
        var range = testFile.Content.GetRangeFromSpan(testFile.Content.GetSpans("ihRegion").Single()).GetSelection();

        testHost.AddFilesToWorkspace(testFile);

        return testHost.GetResponse<InlayHintRequest, InlayHintResponse>(OmniSharpEndpoints.InlayHint, new() { Location = new() { FileName = fileName, Range = range } });
    }

    private static Task<InlayHint> ResolveInlayHint(InlayHint hint, OmniSharpTestHost testHost)
        => testHost.GetResponse<InlayHintResolveRequest, InlayHint>(OmniSharpEndpoints.InlayHintResolve, new() { Hint = hint });

    private KeyValuePair<string, string>[] InlayHintsOptionsToKvp(InlayHintsOptions options)
        => new[]
        {
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.EnableForParameters)}", options.EnableForParameters.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForLiteralParameters)}", options.ForLiteralParameters.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForIndexerParameters)}", options.ForIndexerParameters.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForObjectCreationParameters)}", options.ForObjectCreationParameters.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForOtherParameters)}", options.ForOtherParameters.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatDifferOnlyBySuffix)}", options.SuppressForParametersThatDifferOnlyBySuffix.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatMatchMethodIntent)}", options.SuppressForParametersThatMatchMethodIntent.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatMatchArgumentName)}", options.SuppressForParametersThatMatchArgumentName.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.EnableForTypes)}", options.EnableForTypes.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForImplicitVariableTypes)}", options.ForImplicitVariableTypes.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForLambdaParameterTypes)}", options.ForLambdaParameterTypes.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForImplicitObjectCreation)}", options.ForImplicitObjectCreation.ToString()),
        };
}
