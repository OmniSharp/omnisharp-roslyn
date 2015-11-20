using System;
using System.Composition.Hosting;
using System.Reflection;

namespace OmniSharp.TestCommon
{
    public static class PluginHostHelpers
    {

        public static CompositionHost CreatePluginHost(
            Func<ContainerConfiguration, ContainerConfiguration> configure,
            params Assembly[] assemblies)
        {
            return Startup.ConfigureMef(
                new FakeServiceProvider(),
                new FakeOmniSharpOptions().Value,
                assemblies,
                logger: null,
                configurationAction: configure);
        }

        public static CompositionHost CreatePluginHost(params Assembly[] assemblies)
        {
            return CreatePluginHost(configure: null, assemblies: assemblies);
        }
    }
}
