#nullable enable

using System;
using System.Composition;
using System.Reflection;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.WorkspaceServices
{
    [Shared]
    [ExportWorkspaceServiceFactoryWithAssemblyQualifiedName(
        RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.PickMembers.IPickMembersService")]
    internal sealed class PickMembersWorkspaceServiceFactory : InternalWorkspaceServiceFactory
    {
        [ImportingConstructor]
        public PickMembersWorkspaceServiceFactory()
            : base(RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.PickMembers.IPickMembersService")
        {
        }

        protected override object? Invoke(MethodInfo method, object?[] arguments)
        {
            if (method.Name != "PickMembers")
                throw new MissingMethodException(method.DeclaringType?.FullName, method.Name);

            var resultType = RoslynReflection.GetType(
                RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.PickMembers.PickMembersResult");
            return RoslynReflection.CreateInstance(resultType, arguments[1]!, arguments[2]!, arguments[3]!);
        }
    }
}
