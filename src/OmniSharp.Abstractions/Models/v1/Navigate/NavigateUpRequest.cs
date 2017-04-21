using OmniSharp.Mef;

namespace OmniSharp.Models.Navigate
{
    [OmniSharpEndpoint(OmniSharpEndpoints.NavigateUp, typeof(NavigateUpRequest), typeof(NavigateResponse))]
    public class NavigateUpRequest : Request
    {
    }
}
