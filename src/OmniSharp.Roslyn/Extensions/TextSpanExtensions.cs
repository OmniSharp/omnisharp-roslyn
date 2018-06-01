using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models.V2;

namespace OmniSharp.Roslyn.Extensions
{
    public static class TextSpanExtensions
    {
        public static Range ToRange(this TextSpan textSpan, TextLineCollection lines)
        {
            var line = lines.GetLineFromPosition(textSpan.Start);
            var column = textSpan.Start - line.Start;
            var endLine = lines.GetLineFromPosition(textSpan.End);
            var endColumn = textSpan.End - endLine.Start;

            return new Range()
            {
                Start = new Point() { Column = column, Line = line.LineNumber },
                End = new Point() { Column = endColumn, Line = endLine.LineNumber }
            };
        }
    }
}
