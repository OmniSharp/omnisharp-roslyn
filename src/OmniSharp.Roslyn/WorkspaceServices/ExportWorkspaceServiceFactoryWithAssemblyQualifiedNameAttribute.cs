using System;
using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis.Host.Mef;

namespace OmniSharp
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    public class ExportWorkspaceServiceFactoryWithAssemblyQualifiedNameAttribute : ExportAttribute
    {
        public string ServiceType { get; }
        public string Layer { get; }

        // Theres builtin public attribute for this, but since we target internal types
        // this is needed to build service. MEF doesn't care is it internal or not.
        public ExportWorkspaceServiceFactoryWithAssemblyQualifiedNameAttribute(string typeAssembly, string typeName, string layer = ServiceLayer.Host)
            : base(typeof(IWorkspaceServiceFactory))
        {
            var type = Assembly.Load(typeAssembly).GetType(typeName)
                ?? throw new InvalidOperationException($"Could not resolve '{typeName} from '{typeAssembly}'");

            this.ServiceType = type.AssemblyQualifiedName;
            this.Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        }
    }
}
