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
        public static Lazy<Assembly> LazyLoad(this IOmnisharpAssemblyLoader self, string name)
        {
            return new Lazy<Assembly>(() => self.Load(name));
        }

        public static Assembly Load(this IOmnisharpAssemblyLoader self, string name)
        {
            var assemblyName = name;
            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName = name.Substring(0, name.Length - 4);
            }

            return self.Load(new AssemblyName(assemblyName));
        }

        public static IEnumerable<Assembly> Load(this IOmnisharpAssemblyLoader self, params string[] names)
        {
            foreach (var name in names)
            {
                yield return Load(self, name);
            }
        }
    }
}
