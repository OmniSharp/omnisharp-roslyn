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

        public IEnumerable<string> GetPlugins(IApplicationEnvironment appEnv)
        {
            return Directory.GetDirectories(Path.Combine(appEnv.ApplicationBasePath, "plugins"), "*", SearchOption.TopDirectoryOnly)
                .Concat(this._paths)
                .SelectMany(x => Directory.GetFiles(x, "*.dll", SearchOption.TopDirectoryOnly))
                .ToArray();
        }
    }
}
