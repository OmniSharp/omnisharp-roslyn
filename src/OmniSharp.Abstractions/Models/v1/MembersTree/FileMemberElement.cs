using System;
using System.Collections.Generic;

namespace OmniSharp.Models.MembersTree
{
    public class FileMemberElement : IComparable<FileMemberElement>
    {
        public IEnumerable<FileMemberElement> ChildNodes { get; set; }

        public QuickFix Location { get; set; }

        public int AttributeSpanStart { get; set; }

        public int AttributeSpanEnd { get; set; }

        public string Kind { get; set; }
        
        public ICollection<SyntaxFeature> Features { get; } = new List<SyntaxFeature>();

        public IEnumerable<string> Projects { get; set; }

        public int CompareTo(FileMemberElement other)
        {
            if (other.Location.Line < Location.Line)
            {
                return 1;
            }
            else if (other.Location.Line > Location.Line)
            {
                return -1;
            }
            // same start line
            else if (other.Location.Column < Location.Column)
            {
                return 1;
            }
            else if (other.Location.Column > Location.Column)
            {
                return -1;
            }
            // same start line and start column
            else if (other.Location.EndLine < Location.EndLine)
            {
                return 1;
            }
            else if (other.Location.EndLine > Location.EndLine)
            {
                return -1;
            }
            // same start line, start column, and end line
            else if (other.Location.EndColumn < Location.EndColumn)
            {
                return 1;
            }
            else if (other.Location.EndColumn > Location.EndColumn)
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
                && node.Location.Line == Location.Line
                && node.Location.Column == Location.Column
                && node.Location.EndLine == Location.EndLine
                && node.Location.EndColumn == Location.EndColumn;
        }

        public override int GetHashCode()
        {
            return 13 * Location.Line +
                17 * Location.Column +
                23 * Location.EndLine +
                31 * Location.EndColumn;
        }
    }
}
