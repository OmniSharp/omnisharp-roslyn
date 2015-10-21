using System.Collections.Generic;

namespace OmniSharp.Models.V2
{
    public class GetCodeActionsResponse
    {
        public IEnumerable<OmniSharpCodeAction> CodeActions { get; set; }
    }
}
