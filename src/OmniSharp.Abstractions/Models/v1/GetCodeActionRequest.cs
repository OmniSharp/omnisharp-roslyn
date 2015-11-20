using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.GetCodeAction, typeof(GetCodeActionRequest), typeof(GetCodeActionsResponse))]
    public class GetCodeActionRequest : CodeActionRequest { }
}
