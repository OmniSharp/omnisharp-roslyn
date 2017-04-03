using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Host.Internal
{
    internal static class ConfigurationBuilderExtensions
    {
        internal static void CreateAndAddGlobalOptionsFile(this IConfigurationBuilder configBuilder, IOmniSharpEnvironment env)
        {
            if (env?.SharedDirectory == null) return;

            try
            {
                if (!Directory.Exists(env.SharedDirectory))
                {
                    Directory.CreateDirectory(env.SharedDirectory);
                }

                configBuilder.AddJsonFile(
                    new PhysicalFileProvider(env.SharedDirectory).WrapForPolling(),
                    Constants.OptionsFile,
                    optional: true,
                    reloadOnChange: true);
            }
            catch (Exception e)
            {
                // at this point we have no ILogger yet
                Console.Error.WriteLine($"There was an error when trying to create a global '{Constants.OptionsFile}' file in '{env.SharedDirectory}'. {e.ToString()}");
            }
        }
    }
}