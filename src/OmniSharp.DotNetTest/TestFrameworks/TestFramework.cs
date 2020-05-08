using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OmniSharp.DotNetTest.TestFrameworks
{
    internal abstract class TestFramework
    {
        static TestFramework()
        {
            var builder = ImmutableArray.CreateBuilder<TestFramework>();

            var nunit = new NUnitTestFramework();
            var xunit = new XunitTestFramework();
            var mstest = new MSTestFramework();

            builder.Add(nunit);
            builder.Add(xunit);
            builder.Add(mstest);

            Frameworks = builder.ToImmutable();
        }

        public static TestFramework GetFramework(string name)
        {
            foreach (var framework in Frameworks)
            {
                if (framework.Name == name)
                {
                    return framework;
                }
            }

            return null;
        }

        public static ImmutableArray<TestFramework> Frameworks { get; }

        public abstract string FeatureName { get; }
        public abstract string Name { get; }
        public abstract string MethodArgument { get; }

        protected abstract bool IsTestAttributeName(string typeName);

        public bool IsTestAttribute(INamedTypeSymbol symbol)
        {
            while (symbol != null && symbol.SpecialType != SpecialType.System_Object)
            {
                var typeName = !symbol.ContainingNamespace.IsGlobalNamespace
                    ? $"{symbol.ContainingNamespace}.{symbol.Name}"
                    : symbol.Name;

                if (IsTestAttributeName(typeName))
                {
                    return true;
                }

                symbol = symbol.BaseType;
            }

            return false;
        }

        public bool IsTestMethod(IMethodSymbol symbol)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (IsTestAttribute(attribute.AttributeClass))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsTestMethod(MethodDeclarationSyntax methodDeclaration, SemanticModel sematicModel)
        {
            foreach (var attributeList in methodDeclaration.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var typeSymbol = sematicModel.GetTypeInfo(attribute).Type;

                    while (typeSymbol != null && typeSymbol.SpecialType != SpecialType.System_Object)
                    {
                        var typeName = !typeSymbol.ContainingNamespace.IsGlobalNamespace
                            ? $"{typeSymbol.ContainingNamespace}.{typeSymbol.Name}"
                            : typeSymbol.Name;

                        if (IsTestAttributeName(typeName))
                        {
                            return true;
                        }

                        typeSymbol = typeSymbol.BaseType;
                    }
                }
            }

            return false;
        }
    }
}
