using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Models;
using OmniSharp.Models.CodeFormat;
using OmniSharp.Models.Format;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services;
using OmniSharp.Roslyn.CSharp.Services.Formatting;
using OmniSharp.Roslyn.CSharp.Workers.Formatting;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FormattingFacts : AbstractTestFixture
    {
        public FormattingFacts(ITestOutputHelper output)
            : base(output)
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
        public async Task FormatRespectsIndentationSize()
        {
            var testFile = new TestFile("dummy.cs", "namespace Bar\n{\n    class Foo {}\n}");

            using (var host = CreateOmniSharpHost(testFile))
            {
                var optionsProvider = new CSharpWorkspaceOptionsProvider();

                host.Workspace.Options = optionsProvider.Process(host.Workspace.Options,
                    new FormattingOptions
                    {
                        NewLine = "\n",
                        IndentationSize = 1
                    });

                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal("namespace Bar\n{\n class Foo { }\n}", response.Buffer);
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

            using (var host = CreateOmniSharpHost(testFile))
            {
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

                var requestHandler = host.GetRequestHandler<FormatRangeService>(OmniSharpEndpoints.FormatRange);

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
        }
    }
}
