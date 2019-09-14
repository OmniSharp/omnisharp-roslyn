using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;

namespace OmniSharp
{
    [Shared]
    [ExportWorkspaceServiceFactoryWithAssemblyQualifiedName("Microsoft.CodeAnalysis.Features", "Microsoft.CodeAnalysis.PickMembers.IPickMembersService")]
    public class PickMemberWorkspaceServiceFactory : IWorkspaceServiceFactory
    {
        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            // Generates proxy class to get around issue that IPickMembersService is internal at this point.
            var internalType = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.PickMembers.IPickMembersService");
            return (IWorkspaceService)typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create)).MakeGenericMethod(internalType, typeof(PickMemberWorkspaceService)).Invoke(null, null);
        }
    }
}