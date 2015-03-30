using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
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


        [Fact]
        public async Task TextChangesAreSortedLastFirstWhenFormattingOneLine()
        {
            var source = 
@"class Program
{
    public static void Main(){
>       Thread.Sleep( 25000);<
    }
}";
            await AssertTextChanges(source,
                new LinePositionSpanTextChange() { StartLine = 4, StartColumn = 21, EndLine = 4, EndColumn = 22, NewText = "" },
                new LinePositionSpanTextChange() { StartLine = 4, StartColumn = 8, EndLine = 4, EndColumn = 8, NewText = " " });
        }

        [Fact]
        public async Task TextChangesAreSortedLastFirstWhenFormattingTwoLines()
        {
            var source =
@"class Program
{
>    public static void Main(){
       Thread.Sleep( 25000);<
    }
}";
            await AssertTextChanges(source,
                new LinePositionSpanTextChange() { StartLine = 4, StartColumn = 21, EndLine = 4, EndColumn = 22, NewText = "" },
                new LinePositionSpanTextChange() { StartLine = 4, StartColumn = 8, EndLine = 4, EndColumn = 8, NewText = " " },
                new LinePositionSpanTextChange() { StartLine = 3, StartColumn = 30, EndLine = 3, EndColumn = 30, NewText = "    " });
        }

        private static FormatRangeRequest NewRequest(string source)
        {
            var startLoc = TestHelpers.GetLineAndColumnFromIndex(source, source.IndexOf(">"));
            source = source.Replace(">", string.Empty);
            var endLoc = TestHelpers.GetLineAndColumnFromIndex(source, source.IndexOf("<"));
            source = source.Replace("<", string.Empty);

            return new FormatRangeRequest() {
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
            var actualEnumer = actual.GetEnumerator();

            Assert.Equal(expected.Length, actual.Count());

            for (var i = 0; actualEnumer.MoveNext(); i++)
            {
                Assert.Equal(expected[i].StartLine, actualEnumer.Current.StartLine);
                Assert.Equal(expected[i].StartColumn, actualEnumer.Current.StartColumn);
                Assert.Equal(expected[i].EndLine, actualEnumer.Current.EndLine);
                Assert.Equal(expected[i].EndColumn, actualEnumer.Current.EndColumn);
                Assert.Equal(expected[i].NewText, actualEnumer.Current.NewText);
            }
        }

        private static async Task<IEnumerable<LinePositionSpanTextChange>> FormattingChangesForRange(FormatRangeRequest req)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(req.Buffer, req.FileName);
            var controller = new OmnisharpController(workspace, null);
            
            return (await controller.FormatRange(req)).Changes;
        }
    }
}