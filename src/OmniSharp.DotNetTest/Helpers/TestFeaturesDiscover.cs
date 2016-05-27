using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Models;
using OmniSharp.Abstractions.Services;

namespace OmniSharp.DotNetTest.Helpers
{
    [Export(typeof(ISyntaxFeaturesDiscover))]
    public class TestFeaturesDiscover : ISyntaxFeaturesDiscover
    {
        private static readonly string FeatureName = "XunitTestMethod";

        public bool NeedSemanticModel { get; } = true;

        public IEnumerable<SyntaxFeature> Discover(SyntaxNode node, SemanticModel semanticModel)
        {
            if (node is MethodDeclarationSyntax)
            {
                var method = node as MethodDeclarationSyntax;
                if (IsTestMethod(method, semanticModel))
                {
                    var methodName = semanticModel.GetDeclaredSymbol(node).ToDisplayString();
                    methodName = methodName.Substring(0, methodName.IndexOf('('));

                    yield return new SyntaxFeature
                    {
                        Name = FeatureName,
                        Data = methodName
                    };
                }
            }
        }

        private static bool IsTestMethod(MethodDeclarationSyntax node,
                                         SemanticModel sematicModel)
        {
            return node.DescendantNodes()
                       .OfType<AttributeSyntax>()
                       .Select(attr => sematicModel.GetTypeInfo(attr).Type)
                       .Any(IsDerivedFromFactAttribute);
        }

        private static bool IsDerivedFromFactAttribute(ITypeSymbol symbol)
        {
            string fullName;
            do
            {
                fullName = $"{symbol.ContainingNamespace}.{symbol.Name}";
                if (fullName == "Xunit.FactAttribute")
                {
                    return true;
                }

                symbol = symbol.BaseType;
            } while (symbol.Name != "Object");

            return false;
        }
    }
}