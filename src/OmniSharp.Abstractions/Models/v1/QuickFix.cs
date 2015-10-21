using System.Collections.Generic;
using System.Linq;

namespace OmniSharp.Models
{
    public class QuickFix
    {
        public QuickFix()
        {
            Projects = new List<string>();
        }

        public string FileName { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string Text { get; set; }
        public ICollection<string> Projects { get; set; }

        public override bool Equals(object obj)
        {
            var quickFix = obj as QuickFix;
            if (quickFix == null)
            {
                return false;
            }

            return
                FileName == quickFix.FileName
                && Line == quickFix.Line
                && Column == quickFix.Column
                && EndLine == quickFix.EndLine
                && EndColumn == quickFix.EndColumn
                && Text == quickFix.Text;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + (FileName?.GetHashCode()).GetValueOrDefault();
                hash = hash * 23 + Line.GetHashCode();
                hash = hash * 23 + Column.GetHashCode();
                hash = hash * 23 + EndLine.GetHashCode();
                hash = hash * 23 + EndColumn.GetHashCode();
                hash = hash * 23 + (Text?.GetHashCode()).GetValueOrDefault();
                return hash;
            }
        }
    }
}