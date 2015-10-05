using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.NavigateUp, typeof(NavigateUpRequest), typeof(NavigateResponse))]
    public class NavigateUpRequest : Request
    {
    }
}
