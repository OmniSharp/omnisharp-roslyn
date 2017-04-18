using OmniSharp.Mef;

namespace OmniSharp.Models.GotoRegion
{
    [OmniSharpEndpoint(OmniSharpEndpoints.GotoRegion, typeof(GotoRegionRequest), typeof(QuickFixResponse))]
    public class GotoRegionRequest : Request
    {
    }
}
