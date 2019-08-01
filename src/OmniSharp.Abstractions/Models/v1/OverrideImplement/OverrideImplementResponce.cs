using System.Collections.Generic;

namespace OmniSharp.Models.v1.OverrideImplement
{
    public class OverrideImplementResponce
    {
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
    }
}
