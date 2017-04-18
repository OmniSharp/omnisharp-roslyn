using System.Collections.Generic;

namespace OmniSharp.Models.CodeFormat
{
    public class CodeFormatResponse
    {
        public string Buffer { get; set; }
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
    }
}
