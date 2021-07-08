#nullable enable annotations

namespace OmniSharp.Models.v1.SyntaxTree
{
    public struct SymbolAndKind
    {
        public static SymbolAndKind Null { get; } = new() { Symbol = "<null>", SymbolKind = null };

        public string Symbol { get; set; }
        public string? SymbolKind { get; set; }

        public override string ToString()
        {
            return @$"SymbolAndString {{ {nameof(Symbol)} = ""{Symbol}"", {nameof(SymbolKind)} = {(SymbolKind == null ? "null" : @$"""{SymbolKind}""")} }}";
        }
    }
}
