using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/gotoregion", typeof(GotoRegionRequest), typeof(QuickFixResponse))]
    public class GotoRegionRequest : Request
    {
    }
}
