using System;

namespace OmniSharp.Models
{
    public class QuickFix
    {
        public string LogLevel { get; set; }
        public string FileName { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string Text { get; set; }
    }
}