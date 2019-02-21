using System.Composition;
using System.Reflection;
using Castle.DynamicProxy;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;

namespace OmniSharp
{
    [Shared]
    [ExportWorkspaceServiceFactoryWithAssemblyQualifiedName("Microsoft.CodeAnalysis.Features", "Microsoft.CodeAnalysis.ExtractInterface.IExtractInterfaceOptionsService")]
    public class ExtractInterfaceWorkspaceServiceFactory : IWorkspaceServiceFactory
    {
        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            // IExtractInterfaceOptions service and result types are internal -> workaround with proxy.
            // This service simply passes all members through as selected and doesn't try show UI.
            // When roslyn exposes this interface and members -> remove this workaround.
            ProxyGenerator generator = new ProxyGenerator();
            var internalType = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.ExtractInterface.IExtractInterfaceOptionsService");
            return (IWorkspaceService)generator.CreateInterfaceProxyWithoutTarget(internalType, new[] { typeof(IWorkspaceService)}, new ExtractInterfaceWorkspaceService());
        }
    }
}