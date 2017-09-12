using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using OmniSharp.Internal;
using OmniSharp.Utilities;

namespace OmniSharp
{
    public class ConfigurationBuilder : IConfigurationBuilder
    {
        private readonly IOmniSharpEnvironment _environment;
        private readonly IConfigurationBuilder _builder;

        public ConfigurationBuilder(IOmniSharpEnvironment environment)
        {
            _environment = environment;
            _builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(Constants.ConfigFile, optional: true);
        }

        public IConfigurationBuilder Add(IConfigurationSource source)
        {
            _builder.Add(source);
            return this;
        }

        public IConfigurationRoot Build()
        {
            var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(Constants.ConfigFile, optional: true)
                .AddEnvironmentVariables("OMNISHARP_");

            if (_environment.AdditionalArguments?.Length > 0)
            {
                configBuilder.AddCommandLine(_environment.AdditionalArguments);
            }

            // Use the global omnisharp config if there's any in the shared path
            configBuilder.CreateAndAddGlobalOptionsFile(_environment);

            // Use the local omnisharp config if there's any in the root path
            configBuilder.AddJsonFile(
                new PhysicalFileProvider(_environment.TargetDirectory).WrapForPolling(),
                Constants.OptionsFile,
                optional: true,
                reloadOnChange: true);

            return configBuilder.Build();
        }

        public Dictionary<string, object> Properties => _builder.Properties;
        public IEnumerable<IConfigurationSource> Sources => _builder.Sources;
    }
}
