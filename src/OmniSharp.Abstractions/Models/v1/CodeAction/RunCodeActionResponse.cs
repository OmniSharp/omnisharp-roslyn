using System.Collections.Generic;

namespace OmniSharp.Models.CodeAction
{
    public class RunCodeActionResponse
    {
        public string Text { get; set; }
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
    }
}
