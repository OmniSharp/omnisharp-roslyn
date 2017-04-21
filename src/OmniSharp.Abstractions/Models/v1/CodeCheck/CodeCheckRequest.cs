using OmniSharp.Mef;

namespace OmniSharp.Models.CodeCheck
{
    [OmniSharpEndpoint(OmniSharpEndpoints.CodeCheck, typeof(CodeCheckRequest), typeof(QuickFixResponse))]
    public class CodeCheckRequest : Request
    {
    }
}
