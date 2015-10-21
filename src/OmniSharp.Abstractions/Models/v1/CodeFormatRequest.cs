using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.CodeFormat, typeof(CodeFormatRequest), typeof(CodeFormatResponse))]
    public class CodeFormatRequest : Request
    {
    }
}
