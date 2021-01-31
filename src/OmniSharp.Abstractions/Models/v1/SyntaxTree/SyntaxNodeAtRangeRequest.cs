#nullable enable annotations

using OmniSharp.Mef;
using OmniSharp.Models.V2;

namespace OmniSharp.Models.v1.SyntaxTree
{
    [OmniSharpEndpoint(OmniSharpEndpoints.SyntaxNodeAtRange, typeof(SyntaxNodeAtRangeRequest), typeof(SyntaxNodeAtRangeResponse))]
    public class SyntaxNodeAtRangeRequest : SimpleFileRequest
    {
        public Range Range { get; set; }
    }
}
