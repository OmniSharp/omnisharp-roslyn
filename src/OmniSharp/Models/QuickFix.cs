using System;
using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class QuickFix
    {
        public QuickFix()
        {
            Projects = new List<string>();
        }

        public string LogLevel { get; set; }
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

            return LogLevel == quickFix.LogLevel
                && FileName == quickFix.FileName
                && Line == quickFix.Line
                && Column == quickFix.Column
                && EndLine == quickFix.EndLine
                && EndColumn == quickFix.EndColumn
                && Text == quickFix.Text;
        }

        public override int GetHashCode()
        {
            return LogLevel.GetHashCode()
                ^ FileName.GetHashCode()
                ^ Line.GetHashCode()
                ^ Column.GetHashCode()
                ^ EndLine.GetHashCode()
                ^ EndColumn.GetHashCode()
                ^ Text.GetHashCode();
        }
    }
}