using OmniSharp.Mef;

#nullable enable

namespace OmniSharp.Models.v1.SyntaxTree
{
    [OmniSharpEndpoint(OmniSharpEndpoints.SyntaxTree, typeof(SyntaxTreeRequest), typeof(SyntaxTreeResponse))]
    public class SyntaxTreeRequest : SimpleFileRequest
    {
        public SyntaxTreeNode? Parent { get; set; }
    }
}
