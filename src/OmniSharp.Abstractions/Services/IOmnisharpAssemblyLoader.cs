using System;
using System.Collections.Generic;
using System.Reflection;

namespace OmniSharp.Services
{
    public interface IOmnisharpAssemblyLoader
    {
        Assembly Load(AssemblyName name);
    }

    public static class IOmniSharpAssemblyLoaderExtensions
    {
        public static Lazy<Assembly> LazyLoad(this IOmnisharpAssemblyLoader loader, string assemblyName)
        {
            return new Lazy<Assembly>(() => loader.Load(assemblyName));
        }

        public static Assembly Load(this IOmnisharpAssemblyLoader loader, string name)
        {
            var assemblyName = name;
            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName = name.Substring(0, name.Length - 4);
            }

            return loader.Load(new AssemblyName(assemblyName));
        }

        public static IEnumerable<Assembly> Load(this IOmnisharpAssemblyLoader loader, params string[] assemblyNames)
        {
            foreach (var name in assemblyNames)
            {
                yield return Load(loader, name);
            }
        }
    }
}
