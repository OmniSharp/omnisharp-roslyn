using OmniSharp.Mef;

namespace OmniSharp.Models.Navigate
{
    [OmniSharpEndpoint(OmniSharpEndpoints.NavigateDown, typeof(NavigateDownRequest), typeof(NavigateResponse))]
    public class NavigateDownRequest : Request
    {
    }
}
