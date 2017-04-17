using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.NavigateDown, typeof(NavigateDownRequest), typeof(NavigateResponse))]
    public class NavigateDownRequest : Request
    {
    }
}
