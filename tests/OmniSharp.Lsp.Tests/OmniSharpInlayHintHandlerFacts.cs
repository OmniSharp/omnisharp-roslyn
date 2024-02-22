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
using Xunit.Abstractions;

namespace OmniSharp.Lsp.Tests;

public class OmnisharpInlayHintHandlerFacts : AbstractLanguageServerTestBase
{
    private static readonly IgnoreDataComparer ignoreDataComparer = new IgnoreDataComparer();

    public OmnisharpInlayHintHandlerFacts(ITestOutputHelper output) : base(output)
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

    protected async Task<InlayHintContainer> GetInlayHints(string filename, string source)
    {
        var bufferPath = $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}{filename}";
        var testFile = new TestFile(bufferPath, source);

        OmniSharpTestHost.AddFilesToWorkspace(new[] { testFile });

        var range = testFile.Content.GetRangeFromSpan(testFile.Content.GetSpans("ihRegion").Single()).GetSelection();

        var request = new InlayHintParams
        {
            TextDocument = new TextDocumentIdentifier(bufferPath),
            Range = new Range()
            {
                Start = new Position()
                {
                    Line = range.Start.Line,
                    Character = range.Start.Column
                },
                End = new Position()
                {
                    Line = range.End.Line,
                    Character = range.End.Column
                }
            }
        };

        return await Client.RequestInlayHints(request);
    }

    protected async Task<InlayHint> ResolveInlayHint(InlayHint inlayHint)
    {
        return await Client.ResolveInlayHint(inlayHint);
    }

    protected async Task SetInlayHintOptionsAsync(InlayHintsOptions options)
    {
        await Restart(configurationData: InlayHintsOptionsToDictionary(options));
    }

    private Dictionary<string, string> InlayHintsOptionsToDictionary(InlayHintsOptions options)
        => new()
        {
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.EnableForParameters)}"] = options.EnableForParameters.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForLiteralParameters)}"] = options.ForLiteralParameters.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForIndexerParameters)}"] = options.ForIndexerParameters.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForObjectCreationParameters)}"] = options.ForObjectCreationParameters.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForOtherParameters)}"] = options.ForOtherParameters.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatDifferOnlyBySuffix)}"] = options.SuppressForParametersThatDifferOnlyBySuffix.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatMatchMethodIntent)}"] = options.SuppressForParametersThatMatchMethodIntent.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatMatchArgumentName)}"] = options.SuppressForParametersThatMatchArgumentName.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.EnableForTypes)}"] = options.EnableForTypes.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForImplicitVariableTypes)}"] = options.ForImplicitVariableTypes.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForLambdaParameterTypes)}"] = options.ForLambdaParameterTypes.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForImplicitObjectCreation)}"] = options.ForImplicitObjectCreation.ToString(),
        };

    private class IgnoreDataComparer : IEqualityComparer<InlayHint>
    {
        public bool Equals(InlayHint x, InlayHint y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;

            return x.Position == y.Position
                && x.Label == y.Label
                && x.Tooltip == y.Tooltip
                && TextEditsEqual(x.TextEdits?.ToArray(), y.TextEdits?.ToArray())
                && x.PaddingLeft == y.PaddingLeft
                && x.PaddingRight == y.PaddingRight;
        }

        private static bool TextEditsEqual(TextEdit[] a, TextEdit[] b)
        {
            if (a is null)
            {
                return b is null;
            }

            if (b is null)
            {
                return false;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            for (int index = 0; index < a.Length; index++)
            {
                if (!a[index].Equals(b[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(InlayHint x)
            => (x.Position, x.Label, x.Tooltip, x.TextEdits?.GetHashCode() ?? 0, x.PaddingLeft, x.PaddingRight).GetHashCode();
    }
}
