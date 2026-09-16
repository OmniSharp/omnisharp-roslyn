#nullable enable

using System;
using System.Composition;
using System.Reflection;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.WorkspaceServices
{
    [Shared]
    [ExportWorkspaceServiceFactoryWithAssemblyQualifiedName(
        RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Options.ILegacyGlobalOptionsWorkspaceService")]
    internal sealed class LegacyGlobalOptionsWorkspaceServiceFactory : InternalWorkspaceServiceFactory
    {
        [ImportingConstructor]
        public LegacyGlobalOptionsWorkspaceServiceFactory()
            : base(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Options.ILegacyGlobalOptionsWorkspaceService")
        {
        }

        protected override object? Invoke(MethodInfo method, object?[] arguments)
            => method.Name switch
            {
                "get_RazorUseTabs" => false,
                "get_RazorTabSize" => 4,
                "get_GenerateOverrides" => true,
                "set_GenerateOverrides" => null,
                "GetGenerateEqualsAndGetHashCodeFromMembersGenerateOperators" => false,
                "SetGenerateEqualsAndGetHashCodeFromMembersGenerateOperators" => null,
                "GetGenerateEqualsAndGetHashCodeFromMembersImplementIEquatable" => false,
                "SetGenerateEqualsAndGetHashCodeFromMembersImplementIEquatable" => null,
                "GetGenerateConstructorFromMembersOptionsAddNullChecks" => false,
                "SetGenerateConstructorFromMembersOptionsAddNullChecks" => null,
                "GetSyntaxFormattingOptions" => RoslynReflection.GetStaticProperty(
                    RoslynReflection.GetType(
                        RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Formatting.SyntaxFormattingOptions"),
                    "CommonDefaults"),
                _ => throw new MissingMethodException(method.DeclaringType?.FullName, method.Name)
            };
    }
}
