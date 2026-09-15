#nullable enable

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.WorkspaceServices
{
    [Shared]
    [ExportWorkspaceServiceFactoryWithAssemblyQualifiedName(
        RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.ExtractInterface.IExtractInterfaceOptionsService")]
    internal sealed class ExtractInterfaceWorkspaceServiceFactory : InternalWorkspaceServiceFactory
    {
        [ImportingConstructor]
        public ExtractInterfaceWorkspaceServiceFactory()
            : base(RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.ExtractInterface.IExtractInterfaceOptionsService")
        {
        }

        protected override object? Invoke(MethodInfo method, object?[] arguments)
        {
            if (method.Name != "GetExtractInterfaceOptions")
                throw new MissingMethodException(method.DeclaringType?.FullName, method.Name);

            var members = (ImmutableArray<ISymbol>)arguments[1]!;
            var defaultName = (string)arguments[2]!;
            var optionsType = RoslynReflection.GetType(
                RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceOptionsResult");
            var locationType = optionsType.GetNestedType("ExtractLocation", BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Roslyn extract-interface location type was not found.");
            return RoslynReflection.CreateInstance(
                optionsType, false, members, defaultName, $"{defaultName}.cs", Enum.Parse(locationType, "SameFile"));
        }
    }
}
