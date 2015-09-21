using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/codeformat", typeof(CodeFormatRequest), typeof(CodeFormatResponse))]
    public class CodeFormatRequest : Request
    {
    }
}
