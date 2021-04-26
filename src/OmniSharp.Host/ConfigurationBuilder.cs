using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using OmniSharp.Internal;
using OmniSharp.Utilities;

namespace OmniSharp
{
    public class ConfigurationBuilder
    {
        private readonly IOmniSharpEnvironment _environment;

        public ConfigurationBuilder(IOmniSharpEnvironment environment)
        {
            _environment = environment;
        }

        public ConfigurationResult Build(Action<IConfigurationBuilder> additionalSetup = null)
        {
            try
            {
                var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
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

                // bootstrap additional host configuration at the end
                additionalSetup?.Invoke(configBuilder);

                var config = configBuilder.Build();
                return new ConfigurationResult(config);
            }
            catch (Exception ex)
            {
                return new ConfigurationResult(ex);
            }
        }
    }
}
