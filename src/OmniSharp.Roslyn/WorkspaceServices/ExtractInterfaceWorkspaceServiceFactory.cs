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
            // Generates proxy class to get around issue that IExtractInterfaceOptionsService is internal at this point.
            ProxyGenerator generator = new ProxyGenerator();
            var internalType = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.ExtractInterface.IExtractInterfaceOptionsService");
            return (IWorkspaceService)generator.CreateInterfaceProxyWithoutTarget(internalType, new[] { typeof(IWorkspaceService)}, new ExtractInterfaceWorkspaceService());
        }
    }
}