using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace OmniSharp.Models.Highlight
{
    public class HighlightSpan : IComparable<HighlightSpan>
    {
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int StartLine { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int StartColumn { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int EndLine { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int EndColumn { get; set; }
        public string Kind { get; set; }
        public IEnumerable<string> Projects { get; set; }

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
            var node = other as HighlightSpan;
            return node != null
                && node.StartLine == StartLine
                && node.StartColumn == StartColumn
                && node.EndLine == EndLine
                && node.EndColumn == EndColumn;
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
