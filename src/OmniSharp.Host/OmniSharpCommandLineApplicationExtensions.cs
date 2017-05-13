using System.Linq;
using OmniSharp.Plugins;
using OmniSharp.Services;

namespace OmniSharp
{
    public static class OmniSharpCommandLineApplicationExtensions
    {
        public static OmniSharpEnvironment CreateEnvironment(this OmniSharpCommandLineApplication application)
        {
            return new OmniSharpEnvironment(
                application.ApplicationRoot,
                application.HostPid,
                application.LogLevel,
                application.OtherArgs.ToArray<string>());
        }

        public static PluginAssemblies CreatePluginAssemblies(this OmniSharpCommandLineApplication application)
        {
            return new PluginAssemblies(application.Plugin);
        }
    }
}