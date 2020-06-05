using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Abstractions.Services;
using OmniSharp.DotNetTest.TestFrameworks;
using OmniSharp.Extensions;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest
{
    [Export(typeof(ISyntaxFeaturesDiscover))]
    internal class TestFeaturesDiscover : ISyntaxFeaturesDiscover
    {
        public bool NeedSemanticModel { get; } = true;

        public IEnumerable<SyntaxFeature> Discover(SyntaxNode node, SemanticModel semanticModel)
        {
            if (node is MethodDeclarationSyntax method)
            {
                foreach (var framework in TestFramework.Frameworks)
                {
                    if (framework.IsTestMethod(method, semanticModel))
                    {
                        var methodName = semanticModel.GetDeclaredSymbol(method).GetMetadataName();

                        yield return new SyntaxFeature
                        {
                            Name = framework.FeatureName,
                            Data = methodName
                        };
                    }
                }
            }
        }
    }
}
