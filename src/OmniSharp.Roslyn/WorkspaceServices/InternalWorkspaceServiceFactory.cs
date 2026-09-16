#nullable enable

using System.Reflection;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.WorkspaceServices
{
    internal abstract class InternalWorkspaceServiceFactory : IWorkspaceServiceFactory
    {
        private readonly string _assemblyName;
        private readonly string _interfaceName;

        protected InternalWorkspaceServiceFactory(string assemblyName, string interfaceName)
            => (_assemblyName, _interfaceName) = (assemblyName, interfaceName);

        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            var interfaceType = RoslynReflection.GetType(_assemblyName, _interfaceName);
            return RuntimeWorkspaceServiceAdapter.Create(interfaceType, Invoke);
        }

        protected abstract object? Invoke(MethodInfo method, object?[] arguments);
    }
}
