using System.Collections.Generic;
using System.Linq;

#nullable enable annotations

namespace OmniSharp.Models.v1.SyntaxTree
{
    public class SyntaxNodeInfoResponse
    {
        public SymbolAndKind NodeType { get; set; }
        public string NodeSyntaxKind { get; set; }
        public string? SemanticClassification { get; set; }
        public NodeSymbolInfo? NodeSymbolInfo { get; set; }
        public NodeTypeInfo? NodeTypeInfo { get; set; }
        public SymbolAndKind NodeDeclaredSymbol { get; set; } = SymbolAndKind.Null;
        public Dictionary<string, string> Properties { get; set; }
    }

    public sealed record NodeSymbolInfo
    {
        public SymbolAndKind Symbol { get; set; }
        public string CandidateReason { get; set; }
        public IEnumerable<SymbolAndKind> CandidateSymbols { get; set; }

        public bool Equals(NodeSymbolInfo other)
        {
            return Symbol.Equals(other.Symbol)
                && CandidateReason == other.CandidateReason
                && CandidateSymbols.SequenceEqual(other.CandidateSymbols);
        }

        public override int GetHashCode()
        {
            int hashCode = 1792256067;
            hashCode = hashCode * -1521134295 + Symbol.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(CandidateReason);
            foreach (var sym in CandidateSymbols)
            {
                hashCode = hashCode * -1521134295 + sym.GetHashCode();
            }

            return hashCode;
        }
    }

    public sealed record NodeTypeInfo
    {
        public SymbolAndKind Type { get; set; }
        public SymbolAndKind ConvertedType { get; set; }
        public string Conversion { get; set; }
    }
}
