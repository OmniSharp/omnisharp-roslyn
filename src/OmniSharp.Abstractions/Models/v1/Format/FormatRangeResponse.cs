using System.Collections.Generic;

namespace OmniSharp.Models.Format
{
    public class FormatRangeResponse
    {
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
    }
}