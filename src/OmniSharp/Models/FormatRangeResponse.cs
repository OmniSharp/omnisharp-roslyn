using System.Collections.Generic;

namespace OmniSharp.Models
{
	public class TextEdit
    {
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