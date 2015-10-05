using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.GotoFile, typeof(GotoFileRequest), typeof(QuickFixResponse))]
    public class GotoFileRequest : Request
    {
    }
}
