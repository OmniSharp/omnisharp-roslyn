using System.Collections.Generic;

namespace OmniSharp.Models.V2.CodeActions
{
    public class GetCodeActionsResponse
    {
        public IEnumerable<OmniSharpCodeAction> CodeActions { get; set; }
    }
}
