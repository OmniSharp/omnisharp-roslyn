using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.FindImplementations, typeof(FindImplementationsRequest), typeof(QuickFixResponse))]
    public class FindImplementationsRequest : Request
    {
    }
}
