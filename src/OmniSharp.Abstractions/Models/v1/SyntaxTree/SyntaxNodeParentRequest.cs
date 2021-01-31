using OmniSharp.Mef;

#nullable enable annotations

namespace OmniSharp.Models.v1.SyntaxTree
{
    [OmniSharpEndpoint(OmniSharpEndpoints.SyntaxTreeParentNode, typeof(SyntaxNodeParentRequest), typeof(SyntaxNodeParentResponse))]
    public class SyntaxNodeParentRequest : IRequest
    {
        public SyntaxTreeNode Child { get; set; }
    }
}
