using OmniSharp.Mef;

namespace OmniSharp.Models.GotoFile
{
    [OmniSharpEndpoint(OmniSharpEndpoints.GotoFile, typeof(GotoFileRequest), typeof(QuickFixResponse))]
    public class GotoFileRequest : Request
    {
    }
}
