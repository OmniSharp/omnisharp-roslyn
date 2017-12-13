using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace OmniSharp.Roslyn.CSharp.Services.Signatures
{
    internal class InvocationContext
    {
        public SemanticModel SemanticModel { get; }
        public int Position { get; }
        public SyntaxNode Receiver { get; }
        public IEnumerable<TypeInfo> ArgumentTypes { get; }
        public IEnumerable<SyntaxToken> Separators { get; }
        public bool IsInStaticContext { get; } 

        public InvocationContext(SemanticModel semModel, int position, SyntaxNode receiver, ArgumentListSyntax argList, bool isStatic)
        {
            SemanticModel = semModel;
            Position = position;
            Receiver = receiver;
            ArgumentTypes = argList.Arguments.Select(argument => semModel.GetTypeInfo(argument.Expression));
            Separators = argList.Arguments.GetSeparators();
            IsInStaticContext = isStatic;
        }

        public InvocationContext(SemanticModel semModel, int position, SyntaxNode receiver, AttributeArgumentListSyntax argList, bool isStatic)
        {
            SemanticModel = semModel;
            Position = position;
            Receiver = receiver;
            ArgumentTypes = argList.Arguments.Select(argument => semModel.GetTypeInfo(argument.Expression));
            Separators = argList.Arguments.GetSeparators();
            IsInStaticContext = isStatic;
        }
    }  
}

