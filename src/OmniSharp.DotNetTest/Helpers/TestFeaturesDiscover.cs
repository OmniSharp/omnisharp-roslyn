using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Abstractions.Services;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Helpers
{
    [Export(typeof(ISyntaxFeaturesDiscover))]
    public class TestFeaturesDiscover : ISyntaxFeaturesDiscover
    {
        private static readonly string XunitFeatureName = "XunitTestMethod";
        private static readonly string NUnitFeatureName = "NUnitTestMethod";

        public bool NeedSemanticModel { get; } = true;

        public IEnumerable<SyntaxFeature> Discover(SyntaxNode node, SemanticModel semanticModel)
        {
            if (node is MethodDeclarationSyntax)
            {
                var method = node as MethodDeclarationSyntax;
                bool isTestMethod = false;
                string featureName = null;

                if(IsTestMethod(method, semanticModel, IsDerivedFromFactAttribute))
                {
                    isTestMethod = true;
                    featureName = XunitFeatureName;
                }
                else if (IsTestMethod(method, semanticModel, IsNUnitTest))
                {
                    isTestMethod = true;
                    featureName = NUnitFeatureName;
                }

                if (isTestMethod)
                {
                    var methodName = semanticModel.GetDeclaredSymbol(node).ToDisplayString();
                    methodName = methodName.Substring(0, methodName.IndexOf('('));

                    yield return new SyntaxFeature
                    {
                        Name = featureName,
                        Data = methodName
                    };
                }
            }
        }

        private bool IsTestMethod(MethodDeclarationSyntax node,
                                         SemanticModel sematicModel, Func<ITypeSymbol, bool> predicate)
        {
            return node.DescendantNodes()
                       .OfType<AttributeSyntax>()
                       .Select(attr => sematicModel.GetTypeInfo(attr).Type)
                       .Any(predicate);
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

        private bool IsNUnitTest(ITypeSymbol symbol)
        {
            string fullName;
            do
            {
                fullName = $"{symbol.ContainingNamespace}.{symbol.Name}";
                if (fullName == "NUnit.Framework.TestAttribute"
                    || fullName == "NUnit.Framework.TestCaseAttribute"
                    || fullName == "NUnit.Framework.TestCaseSourceAttribute")
                {
                    return true;
                }

                symbol = symbol.BaseType;
            } while (symbol.Name != "Object");

            return false;
        }
    }
}
