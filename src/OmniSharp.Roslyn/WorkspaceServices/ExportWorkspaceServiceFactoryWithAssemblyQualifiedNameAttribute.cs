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

        public ExportWorkspaceServiceFactoryWithAssemblyQualifiedNameAttribute(string typeAssembly, string typeName, string layer = ServiceLayer.Host)
            : base(typeof(IWorkspaceServiceFactory))
        {
            var type = Assembly.Load(typeAssembly).GetType(typeName)
                ?? throw new InvalidOperationException($"Could not resolve '{typeName} from '{typeAssembly}'");

            Console.WriteLine($"Resolved to type: {type.AssemblyQualifiedName}");
            this.ServiceType = type.AssemblyQualifiedName;
            this.Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        }
    }
}
