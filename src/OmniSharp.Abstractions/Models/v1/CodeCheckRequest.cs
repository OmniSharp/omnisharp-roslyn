using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/codecheck", typeof(CodeCheckRequest), typeof(QuickFixResponse))]
    public class CodeCheckRequest : Request
    {
    }
}
