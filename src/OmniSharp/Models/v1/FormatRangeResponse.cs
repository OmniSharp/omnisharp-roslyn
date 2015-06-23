using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class FormatRangeResponse
    {
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
    }
}