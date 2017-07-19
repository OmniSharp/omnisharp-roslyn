using System;
using Microsoft.CodeAnalysis.Text;
using TestUtility;
using Xunit;

namespace OmniSharp.Tests
{
    public class MarkupCodeFacts
    {
        [Fact]
        public void NoMarkupHasNoPositionAndNoSpans()
        {
            const string code = "class C { }";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);
            Assert.False(markupCode.HasPosition);
            Assert.Throws<InvalidOperationException>(() => { var _ = markupCode.Position; });

            var spans = markupCode.GetSpans();
            Assert.Equal(0, spans.Count);
        }

        [Fact]
        public void PositionAtStartShouldBeZero()
        {
            const string code = "$$class C { }";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);
            Assert.True(markupCode.HasPosition);
            Assert.Equal(0, markupCode.Position);
        }

        [Fact]
        public void PositionAtEndShouldBeSameAsCodeLength()
        {
            const string code = "class C { }$$";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);
            Assert.True(markupCode.HasPosition);
            Assert.Equal(markupCode.Code.Length, markupCode.Position);
        }

        [Fact]
        public void PositionWithInterpolatedString()
        {
            const string code = @"class C { string s = $$$""Hello""; }";
            var markupCode = TestContent.Parse(code);

            Assert.Equal(@"class C { string s = $""Hello""; }", markupCode.Code);
            Assert.True(markupCode.HasPosition);
            Assert.Equal(21, markupCode.Position);
        }

        [Fact]
        public void EmptySpanAtStart()
        {
            const string code = "[||]class C { }";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans = markupCode.GetSpans();
            Assert.Equal(1, spans.Count);
            Assert.Equal(TextSpan.FromBounds(0, 0), spans[0]);
        }

        [Fact]
        public void EmptySpanAtEnd()
        {
            const string code = "class C { }[||]";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans = markupCode.GetSpans();
            Assert.Equal(1, spans.Count);
            Assert.Equal(TextSpan.FromBounds(markupCode.Code.Length, markupCode.Code.Length), spans[0]);
        }

        [Fact]
        public void SpanAroundAllCode()
        {
            const string code = "[|class C { }|]";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans = markupCode.GetSpans();
            Assert.Equal(1, spans.Count);
            Assert.Equal(TextSpan.FromBounds(0, markupCode.Code.Length), spans[0]);

            var spanText = markupCode.Code.Substring(spans[0].Start, spans[0].Length);
            Assert.Equal("class C { }", spanText);
        }

        [Fact]
        public void SpanAroundInnerCode()
        {
            const string code = "clas[|s C {|] }";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans = markupCode.GetSpans();
            Assert.Equal(1, spans.Count);
            Assert.Equal(TextSpan.FromBounds(4, 9), spans[0]);

            var spanText = markupCode.Code.Substring(spans[0].Start, spans[0].Length);
            Assert.Equal("s C {", spanText);
        }

        [Fact]
        public void EmptyNamedSpanAtStart()
        {
            const string code = "{|test:|}class C { }";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans = markupCode.GetSpans("test");
            Assert.Equal(1, spans.Count);
            Assert.Equal(TextSpan.FromBounds(0, 0), spans[0]);
        }

        [Fact]
        public void EmptyNamedSpanAtEnd()
        {
            const string code = "class C { }{|test:|}";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans = markupCode.GetSpans("test");
            Assert.Equal(1, spans.Count);
            Assert.Equal(TextSpan.FromBounds(markupCode.Code.Length, markupCode.Code.Length), spans[0]);
        }

        [Fact]
        public void NamedSpanAroundAllCode()
        {
            const string code = "{|test:class C { }|}";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans = markupCode.GetSpans("test");
            Assert.Equal(1, spans.Count);
            Assert.Equal(TextSpan.FromBounds(0, markupCode.Code.Length), spans[0]);

            var spanText = markupCode.Code.Substring(spans[0].Start, spans[0].Length);
            Assert.Equal("class C { }", spanText);
        }

        [Fact]
        public void NamedSpanAroundInnerCode()
        {
            const string code = "clas{|test:s C {|} }";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans = markupCode.GetSpans("test");
            Assert.Equal(1, spans.Count);
            Assert.Equal(TextSpan.FromBounds(4, 9), spans[0]);

            var spanText = markupCode.Code.Substring(spans[0].Start, spans[0].Length);
            Assert.Equal("s C {", spanText);
        }

        [Fact]
        public void NestedSpans()
        {
            const string code = "[|clas[|s C {|] }|]";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans = markupCode.GetSpans();
            Assert.Equal(2, spans.Count);
            Assert.Equal(TextSpan.FromBounds(0, markupCode.Code.Length), spans[0]);
            Assert.Equal(TextSpan.FromBounds(4, 9), spans[1]);

            var spanText = markupCode.Code.Substring(spans[0].Start, spans[0].Length);
            Assert.Equal("class C { }", spanText);

            spanText = markupCode.Code.Substring(spans[1].Start, spans[1].Length);
            Assert.Equal("s C {", spanText);
        }

        [Fact]
        public void NestedNamedSpans()
        {
            const string code = "{|test:clas{|test:s C {|} }|}";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans = markupCode.GetSpans("test");
            Assert.Equal(2, spans.Count);
            Assert.Equal(TextSpan.FromBounds(0, markupCode.Code.Length), spans[0]);
            Assert.Equal(TextSpan.FromBounds(4, 9), spans[1]);

            var spanText = markupCode.Code.Substring(spans[0].Start, spans[0].Length);
            Assert.Equal("class C { }", spanText);

            spanText = markupCode.Code.Substring(spans[1].Start, spans[1].Length);
            Assert.Equal("s C {", spanText);
        }

        [Fact]
        public void NestedNamedSpansWithDifferentNames()
        {
            const string code = "{|test1:clas{|test2:s C {|} }|}";
            var markupCode = TestContent.Parse(code);

            Assert.Equal("class C { }", markupCode.Code);

            var spans1 = markupCode.GetSpans("test1");
            Assert.Equal(1, spans1.Count);
            Assert.Equal(TextSpan.FromBounds(0, markupCode.Code.Length), spans1[0]);

            var spanText1 = markupCode.Code.Substring(spans1[0].Start, spans1[0].Length);
            Assert.Equal("class C { }", spanText1);

            var spans2 = markupCode.GetSpans("test2");
            Assert.Equal(1, spans2.Count);
            Assert.Equal(TextSpan.FromBounds(4, 9), spans2[0]);

            var spanText2 = markupCode.Code.Substring(spans2[0].Start, spans2[0].Length);
            Assert.Equal("s C {", spanText2);
        }
    }
}
