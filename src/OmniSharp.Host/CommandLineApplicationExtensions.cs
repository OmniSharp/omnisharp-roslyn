using System.Linq;
using Microsoft.Extensions.Configuration;
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
            IConfigurationRoot configuration,
            OmniSharpEnvironment environment)
        {
            var pluginsConfiguration = configuration.GetSection("Plugins");
            var extensionsOptions = new OmniSharpExtensionsOptions();
            ConfigurationBinder.Bind(pluginsConfiguration, extensionsOptions);

            return new PluginAssemblies(application.Plugin.Concat(extensionsOptions.GetNormalizedLocationPaths(environment)));
        }
    }
}
