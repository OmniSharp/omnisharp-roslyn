using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.CodeFormat;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Formatting;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class EditorConfigFacts : AbstractTestFixture
    {
        public EditorConfigFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RespectsDefaultFormatSettings(string filename)
        {
            var testFile = new TestFile(filename, "class Foo\n{\n public Foo()\n}\n}");
            var expected = "class Foo\n{\n    public Foo()\n}\n}";

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "true"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal(expected, response.Buffer);
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RespectsSharedFormatSettings(string filename)
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, filename), "class Foo\n{\n    public Foo()\n}\n}");
            var expected = "class Foo\n{\n public Foo()\n}\n}";

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "true"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal(expected, response.Buffer);
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CanBeDisabled(string filename)
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, filename), @"
class Foo { }
class Bar  :  Foo { }
");
            var expected = @"
class Foo { }
class Bar : Foo { }
";

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "false"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal(expected, response.Buffer);
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RespectsCSharpFormatSettings(string filename)
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, filename), @"
class Foo { }
class Bar  :  Foo { }
");
            var expected = @"
class Foo { }
class Bar:Foo { }
";

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "true"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal(expected, response.Buffer);
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RespectsCSharpFormatSettingsWhenOrganizingUsings(string filename)
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, filename), @"
using Y;
using X;
class Foo { }
class Bar  :  Foo { }
");
            var expected = @"
using X;
using Y;
class Foo { }
class Bar:Foo { }
";

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "true",
                ["FormattingOptions:OrganizeImports"] = "true"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal(expected, response.Buffer);
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RespectsCSharpFormatSettings_InExecutedCodeActions(string filename)
        {
            // omnisharp.json sets spacing to true (1 space)
            // but .editorconfig sets it to false (0 spaces)
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, filename), @"
class Foo { }
class Bar $$   :    Foo { }
");
            var expected = @"
class Foo { }
class Bar:Foo { }
";
            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "true",
                ["FormattingOptions:SpaceAfterColonInBaseTypeDeclaration"] = "true", // this should be ignored because .editorconfig gets higher priority
                ["FormattingOptions:SpaceBeforeColonInBaseTypeDeclaration"] = "true", // this should be ignored because .editorconfig gets higher priority
                ["RoslynExtensionsOptions:EnableAnalyzersSupport"] = "true"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var point = testFile.Content.GetPointFromPosition();
                var runRequestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);
                var runRequest = new RunCodeActionRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName,
                    Identifier = "Fix formatting",
                    WantsTextChanges = false,
                    WantsAllCodeActionOperations = true,
                    Buffer = testFile.Content.Code
                };
                var runResponse = await runRequestHandler.Handle(runRequest);

                Assert.Equal(expected, ((ModifiedFileResponse)runResponse.Changes.First()).Buffer, ignoreLineEndingDifferences: true);
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RespectCSharpCodingConventions(string filename)
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, filename), @"
class Foo
{
    public Foo()
    {
        var number1 = 0
        int number2 = 0;
    }
}");

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "true",
                ["RoslynExtensionsOptions:EnableAnalyzersSupport"] = "true"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var result = await host.RequestCodeCheckAsync();

                Assert.Contains(result.QuickFixes.OfType<DiagnosticLocation>().Where(x => x.FileName == testFile.FileName), f => f.Text == "Use framework type" && f.Id == "IDE0049");
                Assert.Contains(result.QuickFixes.OfType<DiagnosticLocation>().Where(x => x.FileName == testFile.FileName), f => f.Text == "Use explicit type instead of 'var'" && f.Id == "IDE0008");
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RespectNamingConventions(string filename)
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, filename), @"
class Foo
{
    private readonly string _bar;

    public Foo(string bar)
    {
        _bar = bar;
    }
}");

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "true",
                ["RoslynExtensionsOptions:EnableAnalyzersSupport"] = "true"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var result = await host.RequestCodeCheckAsync();

                Assert.Contains(result.QuickFixes.OfType<DiagnosticLocation>().Where(x => x.FileName == testFile.FileName), f => f.Text == "Naming rule violation: Missing prefix: 'xxx_'" && f.Id == "IDE1006");
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RespectNamingConventions_InOfferedRefactorings(string filename)
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, filename), @"public class Foo
{
    public Foo(string som$$ething)
    {
    }
}");

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "true",
                ["RoslynExtensionsOptions:EnableAnalyzersSupport"] = "true"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var point = testFile.Content.GetPointFromPosition();
                var getRequestHandler = host.GetRequestHandler<GetCodeActionsService>(OmniSharpEndpoints.V2.GetCodeActions);
                var getRequest = new GetCodeActionsRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName
                };

                var getResponse = await getRequestHandler.Handle(getRequest);
                Assert.NotNull(getResponse.CodeActions);
                Assert.Contains(getResponse.CodeActions, f => f.Name == "Create and initialize field 'xxx_something'");
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RespectNamingConventions_InExecutedCodeActions(string filename)
        {
            const string code =
                @"public class Foo
                {
                    public Foo(string som$$ething)
                    {
                    }
                }";
            const string expected =
                @"public class Foo
                {
                    private readonly System.String xxx_something;

                    public Foo(string something)
                    {
                        xxx_something = something;
                    }
                }";

            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, filename), code);

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "true",
                ["RoslynExtensionsOptions:EnableAnalyzersSupport"] = "true"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var point = testFile.Content.GetPointFromPosition();
                var runRequestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);
                var runRequest = new RunCodeActionRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName,
                    Identifier = "Create and initialize field 'xxx_something'",
                    WantsTextChanges = false,
                    WantsAllCodeActionOperations = true,
                    Buffer = testFile.Content.Code
                };
                var runResponse = await runRequestHandler.Handle(runRequest);

                AssertIgnoringIndent(expected, ((ModifiedFileResponse)runResponse.Changes.First()).Buffer);
            }
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task RespectNamingConventions_InNamingStyleCodeFixProvider(string filename)
        {
            const string code =
                @"public class Foo
                {
                    private readonly System.String som$$ething;

                    public Foo()
                    {
                    }
                }";
            const string expected =
                @"public class Foo
                {
                    private readonly System.String xxx_something;

                    public Foo()
                    {
                    }
                }";

            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, filename), code);

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["FormattingOptions:EnableEditorConfigSupport"] = "true",
                ["RoslynExtensionsOptions:EnableAnalyzersSupport"] = "true"
            }, TestAssets.Instance.TestFilesFolder))
            {
                var point = testFile.Content.GetPointFromPosition();
                var runRequestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);
                var runRequest = new RunCodeActionRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName,
                    Identifier = "NamingStyleCodeFixProvider",
                    WantsTextChanges = false,
                    WantsAllCodeActionOperations = true,
                    Buffer = testFile.Content.Code
                };
                var runResponse = await runRequestHandler.Handle(runRequest);

                AssertIgnoringIndent(expected, ((ModifiedFileResponse)runResponse.Changes.First()).Buffer);
            }
        }

        private static void AssertIgnoringIndent(string expected, string actual)
        {
            Assert.Equal(TrimLines(expected), TrimLines(actual), false, true, true);
        }

        private static string TrimLines(string source)
        {
            return string.Join("\n", source.Split('\n').Select(s => s.Trim()));
        }
    }
}
