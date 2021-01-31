using OmniSharp.Models.SemanticHighlight;
using System.Collections.Generic;

#nullable enable annotations

namespace OmniSharp.Models.v1.SyntaxTree
{
    public class SyntaxNodeInfoResponse
    {
        public SymbolAndKind NodeType { get; set; }
        public string NodeSyntaxKind { get; set; }
        public string? SemanticClassification { get; set; }
        public SemanticHighlightClassification NodeClassification { get; set; }
        public NodeSymbolInfo? NodeSymbolInfo { get; set; }
        public NodeTypeInfo? NodeTypeInfo { get; set; }
        public SymbolAndKind NodeDeclaredSymbol { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }

    public class NodeSymbolInfo
    {
        public SymbolAndKind Symbol { get; set; }
        public string CandidateReason { get; set; }
        public IEnumerable<SymbolAndKind> CandidateSymbols { get; set; }
    }

    public class NodeTypeInfo
    {
        public SymbolAndKind Type { get; set; }
        public SymbolAndKind ConvertedType { get; set; }
        public string Conversion { get; set; }
    }
}
