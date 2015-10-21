using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OmniSharp.Roslyn.CSharp.Services.Signatures
{
    internal class InvocationContext
    {
        public SemanticModel SemanticModel { get; set; }
        public int Position { get; set; }
        public SyntaxNode Receiver { get; set; }
        public ArgumentListSyntax ArgumentList { get; set; }
    }
}
