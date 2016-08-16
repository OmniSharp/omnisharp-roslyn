using System;
using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class CodeFormatResponse
    {
        public string Buffer { get; set; }
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
    }
}
