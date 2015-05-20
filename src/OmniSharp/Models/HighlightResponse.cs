using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace OmniSharp.Models
{
    public class HighlightResponse : IComparable<HighlightResponse>
    {
        public LinePosition Start { get; set; }
        public LinePosition End { get; set; }
        public string Kind { get; set; }
        public IEnumerable<string> Projects { get; set; }

        internal static HighlightResponse FromClassifiedSpan(ClassifiedSpan span, TextLineCollection lines, IEnumerable<string> projects)
        {
            var linePos = lines.GetLinePositionSpan(span.TextSpan);

            return new HighlightResponse
            {
                Start = linePos.Start,
                End = linePos.End,
                Kind = span.ClassificationType,
                Projects = projects
            };
        }

        public int CompareTo(HighlightResponse other)
        {
            if (other.Start.Line < Start.Line)
            {
                return 1;
            }
            else if (other.Start.Line > Start.Line)
            {
                return -1;
            }
            // same start line
            else if (other.Start.Character < Start.Character)
            {
                return 1;
            }
            else if (other.Start.Character > Start.Character)
            {
                return -1;
            }
            // same start line and start column
            else if (other.End.Line < End.Line)
            {
                return 1;
            }
            else if (other.End.Line > End.Line)
            {
                return -1;
            }
            // same start line, start column, and end line
            else if (other.End.Character < End.Character)
            {
                return 1;
            }
            else if (other.End.Character > End.Character)
            {
                return -1;
            }
            // same, same
            else
            {
                return 0;
            }
        }

        public override bool Equals(object other)
        {
            var node = other as FileMemberElement;
            return node != null
                && node.Location.Line == Start.Line
                && node.Location.Column == Start.Character
                && node.Location.EndLine == End.Line
                && node.Location.EndColumn == End.Character;
        }

        public override int GetHashCode()
        {
            return 13 * Start.Line +
                17 * Start.Character +
                23 * End.Line +
                31 * End.Character;
        }
    }
}
