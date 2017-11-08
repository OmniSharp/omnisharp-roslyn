using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace OmniSharp.Roslyn.CSharp.Services.Signatures
{
    internal class InvocationContext
    {
        public SemanticModel SemanticModel { get; set; }
        public int Position { get; set; }
        public SyntaxNode Receiver { get; set; }
        public IEnumerable<TypeInfo> ArgumentTypes { get; set; }
        public IEnumerable<SyntaxToken> Separators { get; set; }

        public InvocationContext(SemanticModel SemModel,int Pos,SyntaxNode Rec, ArgumentListSyntax ArgList)
        {
            SemanticModel = SemModel;
            Position = Pos;
            Receiver = Rec;
            ArgumentTypes = ArgList.Arguments.Select(argument => SemModel.GetTypeInfo(argument.Expression));
            Separators = ArgList.Arguments.GetSeparators();
        }
        public InvocationContext(SemanticModel SemModel, int Pos, SyntaxNode Rec, AttributeArgumentListSyntax ArgList)
        {
            SemanticModel = SemModel;
            Position = Pos;
            Receiver = Rec;
            ArgumentTypes = ArgList.Arguments.Select(argument => SemModel.GetTypeInfo(argument.Expression));
            Separators = ArgList.Arguments.GetSeparators();
        }
    }  
}

