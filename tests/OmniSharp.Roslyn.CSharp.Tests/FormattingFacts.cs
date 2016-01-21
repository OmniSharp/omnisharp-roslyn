using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Formatting;
using OmniSharp.Roslyn.CSharp.Workers.Format;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{

    public class FormattingFacts
    {
        [Fact]
        public void FindFormatTargetAtCurly()
        {
            AssertFormatTargetKind(SyntaxKind.ClassDeclaration, @"class C {}$");
            AssertFormatTargetKind(SyntaxKind.InterfaceDeclaration, @"interface I {}$");
            AssertFormatTargetKind(SyntaxKind.EnumDeclaration, @"enum E {}$");
            AssertFormatTargetKind(SyntaxKind.StructDeclaration, @"struct S {}$");
            AssertFormatTargetKind(SyntaxKind.NamespaceDeclaration, @"namespace N {}$");

            AssertFormatTargetKind(SyntaxKind.MethodDeclaration, @"
class C {
    public void M(){}$
}");
            AssertFormatTargetKind(SyntaxKind.ObjectInitializerExpression, @"
class C {
    public void M(){

        new T() {
            A = 6,
            B = 7
        }$
    }
}");
            AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
    public void M ()
    {
        for(;;){}$
    }
}");
        }

        [Fact]
        public void FindFormatTargetAtSemiColon()
        {

            AssertFormatTargetKind(SyntaxKind.FieldDeclaration, @"
class C {
    private int F;$
}");
            AssertFormatTargetKind(SyntaxKind.LocalDeclarationStatement, @"
class C {
    public void M()
    {
        var a = 1234;$
    }
}");
            AssertFormatTargetKind(SyntaxKind.ReturnStatement, @"
class C {
    public int M()
    {
        return 1234;$
    }
}");

            AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
    public void M ()
    {
        for(var i = 0;$)
    }
}");
            AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
    public void M ()
    {
        for(var i = 0;$) {}
    }
}");
            AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
    public void M ()
    {
        for(var i = 0; i < 8;$)
    }
}");
        }

        private void AssertFormatTargetKind(SyntaxKind kind, string source)
        {
            var tuple = GetTreeAndOffset(source);
            var target = Formatting.FindFormatTarget(tuple.Item1, tuple.Item2);
            if (target == null)
            {
                Assert.Null(kind);
            }
            else
            {
                Assert.Equal(kind, target.Kind());
            }
        }

        private Tuple<SyntaxTree, int> GetTreeAndOffset(string value)
        {
            var idx = value.IndexOf('$');
            if (idx <= 0)
            {
                Assert.True(false);
            }
            value = value.Remove(idx, 1);
            idx = idx - 1;
            return Tuple.Create(CSharpSyntaxTree.ParseText(value), idx);
        }


        [Fact(Skip = "Broke during update to rc2, pending investigation")]
        public async Task TextChangesAreSortedLastFirst_SingleLine()
        {
            var source = new[]{
                "class Program",
                "{",
                "    public static void Main(){",
                ">       Thread.Sleep( 25000);<",
                "    }",
                "}",
            };

            await AssertTextChanges(string.Join(Environment.NewLine, source),
                new LinePositionSpanTextChange() { StartLine = 4, StartColumn = 21, EndLine = 4, EndColumn = 22, NewText = "" },
                new LinePositionSpanTextChange() { StartLine = 4, StartColumn = 8, EndLine = 4, EndColumn = 8, NewText = " " });
        }

        [Fact(Skip = "Broke during update to rc2, pending investigation")]
        public async Task TextChangesAreSortedLastFirst_MultipleLines()
        {
            var source = new[]{
                "class Program",
                "{",
                "    public static void Main()>{",
                "       Thread.Sleep( 25000);<",
                "    }",
                "}",
            };

            await AssertTextChanges(string.Join(Environment.NewLine, source),
                new LinePositionSpanTextChange() { StartLine = 4, StartColumn = 21, EndLine = 4, EndColumn = 22, NewText = "" },
                new LinePositionSpanTextChange() { StartLine = 4, StartColumn = 8, EndLine = 4, EndColumn = 8, NewText = " " },
                new LinePositionSpanTextChange() { StartLine = 3, StartColumn = 30, EndLine = 3, EndColumn = 30, NewText = "\r\n" });
        }

        private static FormatRangeRequest NewRequest(string source)
        {
            var startLoc = TestHelpers.GetLineAndColumnFromIndex(source, source.IndexOf(">"));
            source = source.Replace(">", string.Empty);
            var endLoc = TestHelpers.GetLineAndColumnFromIndex(source, source.IndexOf("<"));
            source = source.Replace("<", string.Empty);

            return new FormatRangeRequest()
            {
                Buffer = source,
                FileName = "a.cs",
                Line = startLoc.Line,
                Column = startLoc.Column,
                EndLine = endLoc.Line,
                EndColumn = endLoc.Column
            };
        }

        private static async Task AssertTextChanges(string source, params LinePositionSpanTextChange[] expected)
        {
            var request = NewRequest(source);
            var actual = await FormattingChangesForRange(request);
            var enumer = actual.GetEnumerator();

            for (var i = 0; enumer.MoveNext(); i++)
            {
                Assert.Equal(expected[i].NewText, enumer.Current.NewText);
                Assert.Equal(expected[i].StartLine, enumer.Current.StartLine);
                Assert.Equal(expected[i].StartColumn, enumer.Current.StartColumn);
                Assert.Equal(expected[i].EndLine, enumer.Current.EndLine);
                Assert.Equal(expected[i].EndColumn, enumer.Current.EndColumn);
            }
        }

        private static async Task<IEnumerable<LinePositionSpanTextChange>> FormattingChangesForRange(FormatRangeRequest req)
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace(req.Buffer, req.FileName);
            RequestHandler<FormatRangeRequest, FormatRangeResponse> controller = new FormatRangeService(workspace, new FormattingOptions());

            return (await controller.Handle(req)).Changes;
        }

        [Fact]
        public async Task FormatRespectsIndentationSize()
        {
            var source = "namespace Bar\n{\n    class Foo {}\n}";

            var workspace = await TestHelpers.CreateSimpleWorkspace(source);
            var controller = new CodeFormatService(workspace, new FormattingOptions
            {
                NewLine = "\n",
                IndentationSize = 1
            });

            var result = await controller.Handle(new CodeFormatRequest
            {
                FileName = "dummy.cs"
            });

            Assert.Equal("namespace Bar\n{\n class Foo { }\n}", result.Buffer);
        }
    }
}
