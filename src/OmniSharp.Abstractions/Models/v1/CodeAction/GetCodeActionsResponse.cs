using System.Collections.Generic;

namespace OmniSharp.Models.CodeAction
{
    public class GetCodeActionsResponse
    {
        public IEnumerable<string> CodeActions { get; set; }
    }
}
