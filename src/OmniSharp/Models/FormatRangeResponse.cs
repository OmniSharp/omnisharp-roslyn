using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class FormatRangeResponse
    {
        public IEnumerable<TextChange> Changes { get; set; }
    }
}