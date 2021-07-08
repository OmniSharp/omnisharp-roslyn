using System.Collections.Generic;

#nullable enable annotations

namespace OmniSharp.Models.v1.SyntaxTree
{
    public class SyntaxTreeResponse
    {
        public IEnumerable<SyntaxTreeNode> TreeItems { get; set; }
    }
}
