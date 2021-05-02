using System.Linq;
using OmniSharp.Options;
using OmniSharp.Plugins;
using OmniSharp.Services;

namespace OmniSharp
{
    public static class CommandLineApplicationExtensions
    {
        public static OmniSharpEnvironment CreateEnvironment(this CommandLineApplication application)
        {
            return new OmniSharpEnvironment(
                application.ApplicationRoot,
                application.HostPid,
                application.LogLevel,
                application.OtherArgs.ToArray<string>());
        }

        public static PluginAssemblies CreatePluginAssemblies(this CommandLineApplication application,
            OmniSharpOptions options,
            OmniSharpEnvironment environment)
        {
            return new PluginAssemblies(application.Plugin.Concat(options.Plugins.GetNormalizedLocationPaths(environment)));
        }
    }
}
