using System;
using System.Collections.Generic;
using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis.Host.Mef;

namespace OmniSharp.Roslyn.WorkspaceServices
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    public class ExportWorkspaceServiceFactoryWithAssemblyQualifiedNameAttribute : ExportAttribute
    {
        public string ServiceType { get; }
        public string Layer { get; }
        public IReadOnlyList<string> WorkspaceKinds { get; } = Array.Empty<string>();

        // There is a built-in public attribute for this, but since we target internal types
        // this is needed to build the service. MEF doesn't care if it is internal or not.
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
