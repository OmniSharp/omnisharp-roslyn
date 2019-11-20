using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OmniSharp.Services
{
    public interface IAssemblyLoader
    {
        Assembly Load(AssemblyName name);

        IReadOnlyList<Assembly> LoadAllFrom(string folderPath);

        Assembly LoadFrom(string assemblyPath, bool dontLockAssemblyOnDisk = false);
    }

    public static class IAssemblyLoaderExtensions
    {
        public static Lazy<Assembly> LazyLoad(this IAssemblyLoader loader, string assemblyName)
        {
            return new Lazy<Assembly>(() => loader.Load(assemblyName));
        }

        public static Assembly Load(this IAssemblyLoader loader, string name)
        {
            var assemblyName = name;
            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName = name.Substring(0, name.Length - 4);
            }

            return loader.Load(new AssemblyName(assemblyName));
        }

        public static IEnumerable<Assembly> Load(this IAssemblyLoader loader, params string[] assemblyNames)
        {
            foreach (var name in assemblyNames)
            {
                yield return Load(loader, name);
            }
        }

        public static Assembly LoadByAssemblyNameOrPath(
            this IAssemblyLoader loader,
            string assemblyName)
        {
            if (File.Exists(assemblyName))
            {
                return loader.LoadFrom(assemblyName);
            }
            else
            {
                return loader.Load(assemblyName);
            }
        }

        public static IEnumerable<Assembly> LoadByAssemblyNameOrPath(this IAssemblyLoader loader, ILogger logger, IEnumerable<string>  assemblyNames)
        {
            foreach (var assemblyName in assemblyNames)
            {
                Assembly assembly;
                try
                {
                    assembly = loader.LoadByAssemblyNameOrPath(assemblyName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to load assembly by name or path: {assemblyName}");
                    continue;
                }

                yield return assembly;
            }
        }
    }
}
