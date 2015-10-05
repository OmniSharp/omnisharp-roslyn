using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.GotoRegion, typeof(GotoRegionRequest), typeof(QuickFixResponse))]
    public class GotoRegionRequest : Request
    {
    }
}
