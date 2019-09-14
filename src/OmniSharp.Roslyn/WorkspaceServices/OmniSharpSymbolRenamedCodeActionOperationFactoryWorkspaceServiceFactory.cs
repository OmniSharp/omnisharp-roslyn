using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;

namespace OmniSharp
{
    [Shared]
    [ExportWorkspaceServiceFactoryWithAssemblyQualifiedName("Microsoft.CodeAnalysis.Features", "Microsoft.CodeAnalysis.CodeActions.WorkspaceServices.ISymbolRenamedCodeActionOperationFactoryWorkspaceService")]
    public class OmniSharpSymbolRenamedCodeActionOperationFactoryWorkspaceServiceFactory : IWorkspaceServiceFactory
    {
        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            // Generates proxy class to get around issue that ISymbolRenamedCodeActionOperationFactoryWorkspaceService is internal
            var internalType = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.CodeActions.WorkspaceServices.ISymbolRenamedCodeActionOperationFactoryWorkspaceService");
            var result = (IWorkspaceService)typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create)).MakeGenericMethod(internalType, typeof(OmniSharpSymbolRenamedCodeActionOperationFactoryWorkspaceService)).Invoke(null, null);
            return result;
        }
    }
}
