using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.FindImplementations, typeof(FindImplementationsRequest), typeof(QuickFixResponse))]
    public class FindImplementationsRequest : Request
    {
    }
}
