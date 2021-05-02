using System.Collections.Generic;

namespace OmniSharp.Plugins
{
    public class PluginAssemblies
    {
        public PluginAssemblies(IEnumerable<string> assemblyNames)
        {
            AssemblyNames = assemblyNames;
        }

        public IEnumerable<string> AssemblyNames { get; }
    }
}
