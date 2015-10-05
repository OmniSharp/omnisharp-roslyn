using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.CodeCheck, typeof(CodeCheckRequest), typeof(QuickFixResponse))]
    public class CodeCheckRequest : Request
    {
    }
}
