using Microsoft.CodeAnalysis.Text;

namespace TestUtility
{
    public static class SourceTextExtensions
    {
        public static TextPoint GetPointFromPosition(this SourceText text, int position)
        {
            var line = text.Lines.GetLineFromPosition(position);
            var offset = position - line.Start;

            return new TextPoint(line.LineNumber, offset);
        }

        public static TextRange GetRangeFromSpan(this SourceText text, TextSpan span)
        {
            var startLine = text.Lines.GetLineFromPosition(span.Start);
            var startOffset = span.Start - startLine.Start;
            var endLine = text.Lines.GetLineFromPosition(span.End);
            var endOffset = span.End - endLine.Start;

            return new TextRange(
                start: new TextPoint(startLine.LineNumber, startOffset),
                end: new TextPoint(endLine.LineNumber, endOffset));
        }
    }
}
