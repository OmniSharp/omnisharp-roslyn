using OmniSharp.Mef;

namespace OmniSharp.Models.FindImplementations
{
    [OmniSharpEndpoint(OmniSharpEndpoints.FindImplementations, typeof(FindImplementationsRequest), typeof(QuickFixResponse))]
    public class FindImplementationsRequest : Request
    {
    }
}
