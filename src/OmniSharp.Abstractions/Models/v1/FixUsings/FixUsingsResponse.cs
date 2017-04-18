using System.Collections.Generic;

namespace OmniSharp.Models.FixUsings
{
    public class FixUsingsResponse
    {
        public string Buffer { get; set; }
        public IEnumerable<QuickFix> AmbiguousResults { get; set; }
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
    }
}
