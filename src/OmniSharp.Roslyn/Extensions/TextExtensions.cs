using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
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
        /// Converts a line number and offset to a zero-based position within a <see cref="SourceText"/>.
        /// </summary>
        public static int GetPositionFromLineAndOffset(this SourceText text, int lineNumber, int offset)
            => text.Lines[lineNumber].Start + offset;

        /// <summary>
        /// Converts a <see cref="TextSpan"/> in a <see cref="SourceText"/> to an OmniSharp <see cref="Range"/>.
        /// </summary>
        public static Range GetRangeFromSpan(this SourceText text, TextSpan span)
            => new Range
            {
                Start = text.GetPointFromPosition(span.Start),
                End = text.GetPointFromPosition(span.End)
            };

        public static Models.V2.Location GetLocationFromFileLinePositionSpan(this FileLinePositionSpan linePositionSpan)
            => new()
            {
                FileName = linePositionSpan.Path,
                Range = new()
                {
                    Start = new Point { Line = linePositionSpan.StartLinePosition.Line, Column = linePositionSpan.StartLinePosition.Character },
                    End = new Point { Line = linePositionSpan.EndLinePosition.Line, Column = linePositionSpan.EndLinePosition.Character }
                }
            };

        /// <summary>
        /// Converts an OmniSharp <see cref="Range"/> to a <see cref="TextSpan"/> within a <see cref="SourceText"/>.
        /// </summary>
        public static TextSpan GetSpanFromRange(this SourceText text, Range range)
            => TextSpan.FromBounds(
                start: text.GetPositionFromLineAndOffset(range.Start.Line, range.Start.Column),
                end: text.GetPositionFromLineAndOffset(range.End.Line, range.End.Column));

        /// <summary>
        /// Converts an OmniSharp <see cref="Range"/> to a <see cref="TextSpan"/> within a <see cref="SourceText"/>.
        /// </summary>
        public static TextSpan GetSpanFromLinePositionSpanTextChange(this SourceText text, LinePositionSpanTextChange change)
            => TextSpan.FromBounds(
                start: text.GetPositionFromLineAndOffset(change.StartLine, change.StartColumn),
                end: text.GetPositionFromLineAndOffset(change.EndLine, change.EndColumn));
    }
}
