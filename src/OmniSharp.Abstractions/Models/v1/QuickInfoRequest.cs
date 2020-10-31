using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.QuickInfo, typeof(QuickInfoRequest), typeof(QuickInfoResponse))]
    public class QuickInfoRequest : Request
    {
    }
}
