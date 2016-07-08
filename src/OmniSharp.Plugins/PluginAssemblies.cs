using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.PlatformAbstractions;

namespace OmniSharp.Plugins
{
    public class PluginAssemblies
    {
        private readonly IEnumerable<string> _paths;

        public PluginAssemblies(IEnumerable<string> paths)
        {
            _paths = paths;
        }

        public IEnumerable<string> GetPlugins(ApplicationEnvironment appEnv)
        {
            var pluginPath = Path.Combine(appEnv.ApplicationBasePath, "plugins");
            if (Directory.Exists(pluginPath))
            {
                return Directory.GetDirectories(pluginPath, "*", SearchOption.TopDirectoryOnly)
                    .Concat(this._paths)
                    .SelectMany(x => Directory.GetFiles(x, "*.dll", SearchOption.TopDirectoryOnly))
                    .ToArray();
            }
            return Enumerable.Empty<string>();
        }
    }
}
