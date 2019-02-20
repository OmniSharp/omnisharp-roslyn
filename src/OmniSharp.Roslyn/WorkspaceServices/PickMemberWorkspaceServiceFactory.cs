using System.Composition;
using System.Reflection;
using Castle.DynamicProxy;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;

namespace OmniSharp.Roslyn.WorkspaceServices
{
    [Shared]
    [ExportWorkspaceServiceFactoryWithAssemblyQualifiedName("Microsoft.CodeAnalysis.Features", "Microsoft.CodeAnalysis.PickMembers.IPickMembersService")]
    public class PickMemberWorkspaceServiceFactory : IWorkspaceServiceFactory
    {
        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            ProxyGenerator generator = new ProxyGenerator();
            var internalType = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.PickMembers.IPickMembersService");
            return (IWorkspaceService)generator.CreateInterfaceProxyWithoutTarget(internalType, new[] { typeof(IWorkspaceService)}, new PickMemberWorkspaceService());
        }
    }
}