using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.PlatformAbstractions;
#if !NET451
using System.Runtime.Loader;
#endif

namespace OmniSharp.Plugins
{
    public class PluginAssemblies
    {
        private readonly IEnumerable<string> _paths;
        private readonly IEnumerable<string> _plugins;
        private ImmutableArray<Assembly> _assemblies;

        public PluginAssemblies(IEnumerable<string> paths)
        {
            _paths = paths;
            _plugins = _paths
                .SelectMany(x => Directory.GetFiles(x, "*.dll", SearchOption.TopDirectoryOnly))
                .Where(x => !x.Contains(".Native."))
                .ToArray();
        }

        public IEnumerable<string> Plugins => this._plugins;

        public ImmutableArray<Assembly> Assemblies
        {
            get
            {
                if (this._assemblies == null)
                {
                    this._assemblies = ImmutableArray.CreateRange(
                        this._plugins
#if NET451
                            .Select(AssemblyName.GetAssemblyName)
                            .Select(Assembly.Load)
#else
                            .Select(path =>
                            {
                                var loader = new AssemblyLoader(Path.GetDirectoryName(path));
                                return loader.LoadFromAssemblyPath(path);
                            })
#endif
                            .ToArray()
                    );
                }

                return this._assemblies;
            }
        }
    }

#if NETSTANDARD1_6
    public class AssemblyLoader : AssemblyLoadContext
    {
        private string folderPath;

        public AssemblyLoader(string folderPath)
        {
            this.folderPath = folderPath;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            var deps = DependencyContext.Default;
            var res = deps.CompileLibraries.FirstOrDefault(d => d.Name == assemblyName.Name);
            if (res != null)
            {
                return Assembly.Load(new AssemblyName(res.Name));
            }
            else
            {
                var apiApplicationFileInfo = new FileInfo($"{folderPath}{Path.DirectorySeparatorChar}{assemblyName.Name}.dll");
                if (File.Exists(apiApplicationFileInfo.FullName))
                {
                    var asl = new AssemblyLoader(apiApplicationFileInfo.DirectoryName);
                    return asl.LoadFromAssemblyPath(apiApplicationFileInfo.FullName);
                }
            }
            return Assembly.Load(assemblyName);
        }
    }
#endif
}