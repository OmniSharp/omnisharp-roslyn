using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace OmniSharp.Models
{
    public class TextEdit
    {
        public TextEdit()
        {
            // empty
        }

        public TextEdit(string newText, LinePosition start, LinePosition end)
        {
            NewText = newText;
            StartLine = start.Line + 1;
            StartColumn = start.Character + 1;
            EndLine = end.Line + 1;
            EndColumn = end.Character + 1;
        }

        public string NewText { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
    }

    public class FormatRangeResponse
    {
        public IEnumerable<TextEdit> Edits { get; set; }
    }
}