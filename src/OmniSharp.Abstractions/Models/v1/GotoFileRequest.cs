using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.GotoFile, typeof(GotoFileRequest), typeof(QuickFixResponse))]
    public class GotoFileRequest : Request
    {
    }
}
