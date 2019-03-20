using System.Composition;
using System.Reflection;
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
            var internalType = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.ExtractInterface.IExtractInterfaceOptionsService");
            return (IWorkspaceService)typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create)).MakeGenericMethod(internalType, typeof(ExtractInterfaceWorkspaceService)).Invoke(null, null);
        }
    }
}