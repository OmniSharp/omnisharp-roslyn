using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Models.CodeFormat;
using OmniSharp.Models.Format;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services;
using OmniSharp.Roslyn.CSharp.Services.Formatting;
using OmniSharp.Roslyn.CSharp.Workers.Formatting;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FormattingFacts : AbstractTestFixture
    {
        public FormattingFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Fact]
        public void FindFormatTargetAtCurly()
        {
            AssertFormatTargetKind(SyntaxKind.ClassDeclaration, @"class C {}$$");
            AssertFormatTargetKind(SyntaxKind.InterfaceDeclaration, @"interface I {}$$");
            AssertFormatTargetKind(SyntaxKind.EnumDeclaration, @"enum E {}$$");
            AssertFormatTargetKind(SyntaxKind.StructDeclaration, @"struct S {}$$");
            AssertFormatTargetKind(SyntaxKind.NamespaceDeclaration, @"namespace N {}$$");

            AssertFormatTargetKind(SyntaxKind.MethodDeclaration, @"
class C {
    public void M(){}$$
}");
            AssertFormatTargetKind(SyntaxKind.ObjectInitializerExpression, @"
class C {
    public void M(){

        new T() {
            A = 6,
            B = 7
        }$$
    }
}");
            AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
    public void M ()
    {
        for(;;){}$$
    }
}");
        }

        [Fact]
        public void FindFormatTargetAtSemiColon()
        {

            AssertFormatTargetKind(SyntaxKind.FieldDeclaration, @"
class C {
    private int F;$$
}");
            AssertFormatTargetKind(SyntaxKind.LocalDeclarationStatement, @"
class C {
    public void M()
    {
        var a = 1234;$$
    }
}");
            AssertFormatTargetKind(SyntaxKind.ReturnStatement, @"
class C {
    public int M()
    {
        return 1234;$$
    }
}");

            AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
    public void M ()
    {
        for(var i = 0;$$) {}
    }
}");
            AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
    public void M ()
    {
        for(var i = 0; i < 8;$$)
    }
}");
        }

        [Fact]
        public async Task TextChangesAreSortedLastFirst_SingleLine()
        {
            var source = new[]
            {
                "class Program",
                "{",
                "    public static void Main(){",
                "[|       Thread.Sleep( 25000);|]",
                "    }",
                "}",
            };

            await AssertTextChanges(string.Join("\r\n", source),
                new LinePositionSpanTextChange { StartLine = 3, StartColumn = 20, EndLine = 3, EndColumn = 21, NewText = "" },
                new LinePositionSpanTextChange { StartLine = 2, StartColumn = 30, EndLine = 3, EndColumn = 0, NewText = "\n " });
        }

        [Fact]
        public async Task TextChangesAreSortedLastFirst_MultipleLines()
        {
            var source = new[]
            {
                "class Program",
                "{",
                "    public static void Main()[|{",
                "       Thread.Sleep( 25000);|]",
                "    }",
                "}",
            };

            await AssertTextChanges(string.Join("\r\n", source),
                new LinePositionSpanTextChange { StartLine = 3, StartColumn = 20, EndLine = 3, EndColumn = 21, NewText = "" },
                new LinePositionSpanTextChange { StartLine = 2, StartColumn = 30, EndLine = 3, EndColumn = 0, NewText = "\n " });
        }

        [Fact]
        public async Task TextChangesOnStaringSpanBeforeFirstCharacterInLine()
        {
            var source =
 @"class Program
{
    public static void Main()
    {
         [|          int foo = 1;|]
    }
}";

            var expected =
@"class Program
{
    public static void Main()
    {
        int foo = 1;
    }
}";

            await AssertTextChanges(source, expected);
        }

        [Fact]
        public async Task TextChangesOnStartingSpanAtFirstCharacterInLine()
        {
            var source =
 @"class Program
{
    public static void Main()
    {
                   [|int foo = 1;|]
    }
}";
            var expected =
@"class Program
{
    public static void Main()
    {
        int foo = 1;
    }
}";

            await AssertTextChanges(source, expected);
        }

        [Fact]
        public async Task TextChangesOnStartingSpanAfterFirstCharacterInLine()
        {
            var source =
 @"class Program
{
    public static void Main()
    {
                   i[|nt foo = 1;|]
    }
}";

            var expected =
@"class Program
{
    public static void Main()
    {
        int foo = 1;
    }
}";

            await AssertTextChanges(source, expected);
        }

        [Fact]
        public async Task TextChangesOnStartingSpanAfterFirstCharacterInLineWithMultipleLines()
        {
            var source =
@"class Program
{
    public static void Main()
    {
                i[|nt foo = 1;
                    bool b = false;
                        Console.WriteLine(foo);|]
    }
}";

            var expected =
@"class Program
{
    public static void Main()
    {
        int foo = 1;
        bool b = false;
        Console.WriteLine(foo);
    }
}";

            await AssertTextChanges(source, expected);
        }

        [Fact]
        public async Task FormatRespectsIndentationSize()
        {
            var testFile = new TestFile("dummy.cs", "namespace Bar\n{\n    class Foo {}\n}");

            using (var host = CreateOmniSharpHost(testFile))
            {
                var optionsProvider = new CSharpFormattingWorkspaceOptionsProvider();

                var omnisharpOptions = new OmniSharpOptions();
                omnisharpOptions.FormattingOptions.NewLine = "\n";
                omnisharpOptions.FormattingOptions.IndentationSize = 1;

                host.Workspace.TryApplyChanges(host.Workspace.CurrentSolution.WithOptions(optionsProvider.Process(host.Workspace.Options, omnisharpOptions, new OmniSharpEnvironment())));

                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal("namespace Bar\n{\n class Foo { }\n}", response.Buffer);
            }
        }

        [Fact]
        public async Task OrganizesImports()
        {
            var testFile = new TestFile("dummy.cs", @"
using System.IO;
using Dummy;
using System;

namespace Bar
{
    class Foo { }
}");

            using (var host = CreateOmniSharpHost(new[] { testFile }, configurationData: new Dictionary<string, string>
            {
                ["FormattingOptions:OrganizeImports"] = "true"
            }))
            {
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal(@"
using System;
using System.IO;
using Dummy;

namespace Bar
{
    class Foo { }
}", response.Buffer);
            }
        }

        [Fact]
        public async Task DoesntOrganizeImportsWhenDisabled()
        {
            var testFile = new TestFile("dummy.cs", @"
using System.IO;
using Dummy;
using System;

namespace Bar
{
    class Foo { }
}");

            using (var host = CreateOmniSharpHost(new[] { testFile }, configurationData: new Dictionary<string, string>
            {
                ["FormattingOptions:OrganizeImports"] = "false"
            }))
            {
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal(@"
using System.IO;
using Dummy;
using System;

namespace Bar
{
    class Foo { }
}", response.Buffer);
            }
        }

        private static void AssertFormatTargetKind(SyntaxKind kind, string input)
        {
            var content = TestContent.Parse(input);
            var tree = SyntaxFactory.ParseSyntaxTree(content.Code);
            var root = tree.GetRoot();

            var target = FormattingWorker.FindFormatTarget(root, content.Position);

            Assert.Equal(kind, target.Kind());
        }

        private async Task AssertTextChanges(string source, params LinePositionSpanTextChange[] expected)
        {
            var testFile = new TestFile("dummy.cs", source);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var span = testFile.Content.GetSpans().Single();
            var range = testFile.Content.GetRangeFromSpan(span);

            var request = new FormatRangeRequest()
            {
                Buffer = testFile.Content.Code,
                FileName = testFile.FileName,
                Line = range.Start.Line,
                Column = range.Start.Offset,
                EndLine = range.End.Line,
                EndColumn = range.End.Offset
            };

            var requestHandler = SharedOmniSharpTestHost.GetRequestHandler<FormatRangeService>(OmniSharpEndpoints.FormatRange);

            var response = await requestHandler.Handle(request);
            var actual = response.Changes.ToArray();

            Assert.Equal(expected.Length, actual.Length);

            for (var i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].NewText, actual[i].NewText);
                Assert.Equal(expected[i].StartLine, actual[i].StartLine);
                Assert.Equal(expected[i].StartColumn, actual[i].StartColumn);
                Assert.Equal(expected[i].EndLine, actual[i].EndLine);
                Assert.Equal(expected[i].EndColumn, actual[i].EndColumn);
            }
        }

        private async Task AssertTextChanges(string source, string expected)
        {
            var testFile = new TestFile("dummy.cs", source);

            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var span = testFile.Content.GetSpans().Single();
            var range = testFile.Content.GetRangeFromSpan(span);

            var request = new FormatRangeRequest()
            {
                Buffer = testFile.Content.Code,
                FileName = testFile.FileName,
                Line = range.Start.Line,
                Column = range.Start.Offset,
                EndLine = range.End.Line,
                EndColumn = range.End.Offset
            };

            var requestHandler = SharedOmniSharpTestHost.GetRequestHandler<FormatRangeService>(OmniSharpEndpoints.FormatRange);

            var response = await requestHandler.Handle(request);
            var actual = response.Changes.ToArray();

            var oldText = testFile.Content.Text;
            var textChanges = GetTextChanges(oldText, response.Changes);
            var actualText = oldText.WithChanges(textChanges).ToString();
            Assert.Equal(expected.Replace("\r\n", "\n"), actualText.Replace("\r\n", "\n"));
        }

        public static IEnumerable<TextChange> GetTextChanges(SourceText oldText, IEnumerable<LinePositionSpanTextChange> changes)
        {
            var textChanges = new List<TextChange>();
            foreach (var change in changes)
            {
                var startPosition = new LinePosition(change.StartLine, change.StartColumn);
                var endPosition = new LinePosition(change.EndLine, change.EndColumn);
                var span = oldText.Lines.GetTextSpan(new LinePositionSpan(startPosition, endPosition));
                var newText = change.NewText;
                textChanges.Add(new TextChange(span, newText));
            }

            return textChanges.OrderBy(change => change.Span);
        }
    }
}
