using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.GotoRegion, typeof(GotoRegionRequest), typeof(QuickFixResponse))]
    public class GotoRegionRequest : Request
    {
    }
}
