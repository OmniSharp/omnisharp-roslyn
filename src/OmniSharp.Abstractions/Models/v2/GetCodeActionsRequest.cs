using OmniSharp.Mef;

namespace OmniSharp.Models.V2
{
    [OmniSharpEndpoint("/v2/getcodeactions", typeof(GetCodeActionsRequest), typeof(GetCodeActionsResponse))]
    public class GetCodeActionsRequest : Request, ICodeActionRequest
    {
        public Range Selection { get; set; }
    }
}
