#nullable enable

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.WorkspaceServices
{
    [Shared]
    [ExportWorkspaceServiceFactoryWithAssemblyQualifiedName(
        RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.ExtractClass.IExtractClassOptionsService")]
    internal sealed class ExtractClassWorkspaceServiceFactory : InternalWorkspaceServiceFactory
    {
        [ImportingConstructor]
        public ExtractClassWorkspaceServiceFactory()
            : base(RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.ExtractClass.IExtractClassOptionsService")
        {
        }

        protected override object? Invoke(MethodInfo method, object?[] arguments)
        {
            if (method.Name != "GetExtractClassOptions")
                throw new MissingMethodException(method.DeclaringType?.FullName, method.Name);

            var originalType = (INamedTypeSymbol)arguments[1]!;
            var selectedMembers = (ImmutableArray<ISymbol>)arguments[2]!;
            var members = selectedMembers.IsEmpty
                ? originalType.GetMembers().Where(member => member switch
                {
                    IMethodSymbol methodSymbol => methodSymbol.MethodKind == MethodKind.Ordinary,
                    IFieldSymbol fieldSymbol => !fieldSymbol.IsImplicitlyDeclared,
                    _ => member.Kind == SymbolKind.Property || member.Kind == SymbolKind.Event
                })
                : selectedMembers;

            var resultType = RoslynReflection.GetType(
                RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.ExtractClass.ExtractClassMemberAnalysisResult");
            var results = members.Select(member => RoslynReflection.CreateInstance(resultType, member, false));
            var immutableResults = RoslynReflection.CreateImmutableArray(resultType, results);
            var optionsType = RoslynReflection.GetType(
                RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.ExtractClass.ExtractClassOptions");
            const string name = "NewBaseType";
            return RoslynReflection.CreateInstance(optionsType, $"{name}.cs", name, true, immutableResults);
        }
    }
}
