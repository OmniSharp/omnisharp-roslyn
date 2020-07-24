using OmniSharp.Mef;

namespace OmniSharp.Models.v2
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.QuickInfo, typeof(QuickInfoRequest), typeof(QuickInfoResponse))]
    public class QuickInfoRequest : Request
    {
    }
}
