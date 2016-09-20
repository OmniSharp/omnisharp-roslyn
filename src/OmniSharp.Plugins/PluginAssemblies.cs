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
            var pluginPath = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "plugins");
            if (Directory.Exists(pluginPath))
            {
                _plugins = Directory.GetDirectories(pluginPath, "*", SearchOption.TopDirectoryOnly)
                    .Concat(this._paths)
                    .SelectMany(x => Directory.GetFiles(x, "*.dll", SearchOption.TopDirectoryOnly))
                    .ToArray();
            }
            else
            {
                _plugins = Enumerable.Empty<string>();
            }
        }

        public IEnumerable<string> Plugins
        {
            get
            {
                return this._plugins;
            }
        }

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
                            .Select(path => {
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
            var res = deps.CompileLibraries.Where(d => d.Name.Contains(assemblyName.Name)).ToList();
            if (res.Count > 0)
            {
                return Assembly.Load(new AssemblyName(res.First().Name));
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
