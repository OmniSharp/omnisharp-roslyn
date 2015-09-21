using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/gotofile", typeof(GotoFileRequest), typeof(QuickFixResponse))]
    public class GotoFileRequest : Request
    {
    }
}
