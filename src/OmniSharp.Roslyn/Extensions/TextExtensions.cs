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

            // Note: OmniSharp text coordinates are 1-based by default.
            return new Point
            {
                Line = line.LineNumber + 1,
                Column = position - line.Start + 1
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
