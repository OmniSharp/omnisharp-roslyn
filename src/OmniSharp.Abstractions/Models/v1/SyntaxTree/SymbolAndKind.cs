#nullable enable annotations

namespace OmniSharp.Models.v1.SyntaxTree
{
    public class SymbolAndKind
    {
        public static SymbolAndKind Null { get; } = new() { Symbol = "<null>", SymbolKind = null };

        public string Symbol { get; set; }
        public string? SymbolKind { get; set; }
    }
}
