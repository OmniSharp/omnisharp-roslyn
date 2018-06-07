using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models.V2;

namespace OmniSharp.Extensions
{
    public static class TextExtensions
    {
        /// <summary>
        /// Converts a zero-based position in a <see cref="SourceText"/> to an OmniSharp <see cref="Point"/>.
        /// </summary>
        public static Point GetPointFromPosition(this SourceText text, int position)
        {
            var line = text.Lines.GetLineFromPosition(position);

            return new Point
            {
                Line = line.LineNumber,
                Column = position - line.Start
            };
        }

        /// <summary>
        /// Converts a <see cref="TextSpan"/> in a <see cref="SourceText"/> to an OmniSharp <see cref="Range"/>.
        /// </summary>
        public static Range GetRangeFromSpan(this SourceText text, TextSpan span)
            => new Range
            {
                Start = text.GetPointFromPosition(span.Start),
                End = text.GetPointFromPosition(span.End)
            };
    }
}
