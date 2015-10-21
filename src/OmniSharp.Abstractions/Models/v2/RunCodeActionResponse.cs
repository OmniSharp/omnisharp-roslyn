using System.Collections.Generic;

namespace OmniSharp.Models.V2
{
    public class RunCodeActionResponse
    {
        public IEnumerable<ModifiedFileResponse> Changes { get; set; }
    }
}
