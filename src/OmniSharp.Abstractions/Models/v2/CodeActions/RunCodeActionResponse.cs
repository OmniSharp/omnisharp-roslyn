using System.Collections.Generic;

namespace OmniSharp.Models.V2.CodeActions
{
    public class RunCodeActionResponse
    {
        public IEnumerable<FileOperationResponse> Changes { get; set; }
    }
}
