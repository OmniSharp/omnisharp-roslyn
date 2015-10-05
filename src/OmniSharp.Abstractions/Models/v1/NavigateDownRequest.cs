using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.NavigateDown, typeof(NavigateDownRequest), typeof(NavigateResponse))]
    public class NavigateDownRequest : Request
    {
    }
}
