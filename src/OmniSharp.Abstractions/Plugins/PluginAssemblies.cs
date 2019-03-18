using System.Collections.Generic;

namespace OmniSharp.Plugins
{
    public class PluginAssemblies
    {
        private readonly IEnumerable<string> _assemblyNames;

        public PluginAssemblies(IEnumerable<string> assemblyNames)
        {
            _assemblyNames = assemblyNames;
        }

        public IEnumerable<string> AssemblyNames { get { return _assemblyNames; } }
    }
}
