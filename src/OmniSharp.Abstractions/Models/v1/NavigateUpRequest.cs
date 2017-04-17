using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.NavigateUp, typeof(NavigateUpRequest), typeof(NavigateResponse))]
    public class NavigateUpRequest : Request
    {
    }
}
