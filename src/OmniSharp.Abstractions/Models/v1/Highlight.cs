using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace OmniSharp.Models
{
    public class HighlightSpan : IComparable<HighlightSpan>
    {
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string Kind { get; set; }
        public IEnumerable<string> Projects { get; set; }

        public static HighlightSpan FromClassifiedSpan(ClassifiedSpan span, TextLineCollection lines, IEnumerable<string> projects)
        {
            var linePos = lines.GetLinePositionSpan(span.TextSpan);

            return new HighlightSpan
            {
                StartLine = linePos.Start.Line + 1,
                EndLine = linePos.End.Line + 1,
                StartColumn = linePos.Start.Character + 1,
                EndColumn = linePos.End.Character + 1,
                Kind = span.ClassificationType,
                Projects = projects
            };
        }

        public int CompareTo(HighlightSpan other)
        {
            if (other.StartLine < StartLine)
            {
                return 1;
            }
            else if (other.StartLine > StartLine)
            {
                return -1;
            }
            // same start line
            else if (other.StartColumn < StartColumn)
            {
                return 1;
            }
            else if (other.StartColumn > StartColumn)
            {
                return -1;
            }
            // same start line and start column
            else if (other.EndLine < EndLine)
            {
                return 1;
            }
            else if (other.EndLine > EndLine)
            {
                return -1;
            }
            // same start line, start column, and end line
            else if (other.EndColumn < EndColumn)
            {
                return 1;
            }
            else if (other.EndColumn > EndColumn)
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
                && node.Location.Line == StartLine
                && node.Location.Column == StartColumn
                && node.Location.EndLine == EndLine
                && node.Location.EndColumn == EndColumn;
        }

        public override int GetHashCode()
        {
            return 13 * StartLine +
                17 * StartColumn +
                23 * EndLine +
                31 * EndColumn;
        }
    }
}
