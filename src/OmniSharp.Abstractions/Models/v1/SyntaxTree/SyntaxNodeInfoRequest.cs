using OmniSharp.Mef;

#nullable enable annotations
namespace OmniSharp.Models.v1.SyntaxTree
{
    [OmniSharpEndpoint(OmniSharpEndpoints.SyntaxTreeNodeInfo, typeof(SyntaxNodeInfoRequest), typeof(SyntaxNodeInfoResponse))]
    public class SyntaxNodeInfoRequest : IRequest
    {
        public SyntaxTreeNode Node { get; set; }
    }
}
