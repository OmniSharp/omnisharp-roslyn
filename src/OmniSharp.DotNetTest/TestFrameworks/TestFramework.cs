using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OmniSharp.DotNetTest.TestFrameworks
{
    internal abstract class TestFramework
    {
        private static readonly ImmutableDictionary<string, TestFramework> s_frameworks;

        static TestFramework()
        {
            var builder = ImmutableDictionary.CreateBuilder<string, TestFramework>();

            var nunit = new NUnitTestFramework();
            var xunit = new XunitTestFramework();
            var mstest = new MSTestFramework();

            builder.Add(nunit.Name, nunit);
            builder.Add(xunit.Name, xunit);
            builder.Add(mstest.Name, mstest);

            s_frameworks = builder.ToImmutable();
        }

        public static TestFramework GetFramework(string name)
        {
            return s_frameworks.TryGetValue(name, out var result)
                ? result
                : null;
        }

        public static IEnumerable<TestFramework> GetFrameworks()
        {
            foreach (var kvp in s_frameworks)
            {
                yield return kvp.Value;
            }
        }

        public abstract string FeatureName { get; }
        public abstract string Name { get; }
        public abstract string MethodArgument { get; }

        protected abstract bool IsTestAttributeName(string typeName);

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
