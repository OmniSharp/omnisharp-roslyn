using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class RunCodeActionResponse
    {
        public string Text { get; set; }
        public IEnumerable<ModifiedFileResponse> Changes { get; set; }
    }
}
