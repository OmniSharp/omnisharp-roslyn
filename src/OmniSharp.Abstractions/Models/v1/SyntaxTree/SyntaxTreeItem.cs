#nullable enable annotations

using OmniSharp.Models.V2;

namespace OmniSharp.Models.v1.SyntaxTree
{
    public record SyntaxTreeNode
    {
        public SymbolAndKind NodeType { get; set; }
        public Range Range { get; set; }
        public bool HasChildren { get; set; }
        public int Id { get; set; }
    }
}
