using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace OmniSharp.Roslyn.CSharp.Services.Signatures
{


    internal class InvocationContext
    {
        public SemanticModel SemanticModel { get; set; }
        public int Position { get; set; }
        public SyntaxNode Receiver { get; set; }
        public IEnumerable<TypeInfo> ArgumentTypes { get; set; }
        public IEnumerable<SyntaxToken> Separators { get; set; } 
    }


    
}

