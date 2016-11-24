using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Formatting;
using OmniSharp.Roslyn.CSharp.Workers.Formatting;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FormattingFacts
    {
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
            var source = "namespace Bar\n{\n    class Foo {}\n}";

            var workspace = await TestHelpers.CreateSimpleWorkspace(source);
            var controller = new CodeFormatService(workspace,
                new FormattingOptions
                {
                    NewLine = "\n",
                    IndentationSize = 1
                });

            var result = await controller.Handle(
                new CodeFormatRequest
                {
                    FileName = "dummy.cs"
                });

            Assert.Equal("namespace Bar\n{\n class Foo { }\n}", result.Buffer);
        }

        private static void AssertFormatTargetKind(SyntaxKind kind, string input)
        {
            var markup = MarkupCode.Parse(input);
            var tree = SyntaxFactory.ParseSyntaxTree(markup.Code);
            var root = tree.GetRoot();

            var target = FormattingWorker.FindFormatTarget(root, markup.Position);

            Assert.Equal(kind, target.Kind());
        }

        private static async Task AssertTextChanges(string source, params LinePositionSpanTextChange[] expected)
        {
            var request = CreateRequest(source);

            var workspace = await TestHelpers.CreateSimpleWorkspace(request.Buffer, request.FileName);
            var controller = new FormatRangeService(workspace, new FormattingOptions());

            var response = await controller.Handle(request);
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

        private static FormatRangeRequest CreateRequest(string input)
        {
            var markup = MarkupCode.Parse(input);
            var span = markup.GetSpans().Single();

            var startLine = markup.Text.Lines.GetLineFromPosition(span.Start);
            var startColumn = span.Start - startLine.Start;

            var endLine = markup.Text.Lines.GetLineFromPosition(span.End);
            var endColumn = span.End - endLine.Start;

            return new FormatRangeRequest()
            {
                Buffer = markup.Code,
                FileName = "a.cs",
                Line = startLine.LineNumber,
                Column = startColumn,
                EndLine = endLine.LineNumber,
                EndColumn = endColumn
            };
        }
    }
}
