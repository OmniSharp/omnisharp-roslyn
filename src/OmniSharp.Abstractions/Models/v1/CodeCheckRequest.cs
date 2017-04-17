using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.CodeCheck, typeof(CodeCheckRequest), typeof(QuickFixResponse))]
    public class CodeCheckRequest : Request
    {
    }
}
