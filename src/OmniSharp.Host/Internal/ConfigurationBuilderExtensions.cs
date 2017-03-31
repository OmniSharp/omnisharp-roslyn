using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using OmniSharp.Services;
using System;
using System.IO;

namespace OmniSharp.Host.Internal
{
    internal static class ConfigurationBuilderExtensions
    {
        internal static void CreateAndAddGlobalOptionsFile(this IConfigurationBuilder configBuilder, IOmniSharpEnvironment env)
        {
            if (env?.SharedDirectoryPath == null) return;

            try
            {
                if (!Directory.Exists(env.SharedDirectoryPath))
                {
                    Directory.CreateDirectory(env.SharedDirectoryPath);
                }

                var omnisharpGlobalFilePath = Path.Combine(env.SharedDirectoryPath, Constants.OptionsFile);
                configBuilder.AddJsonFile(
                    new PhysicalFileProvider(env.SharedDirectoryPath),
                    Constants.OptionsFile,
                    optional: true,
                    reloadOnChange: true);
            }
            catch (Exception e)
            {
                // at this point we have no ILogger yet
                Console.Error.WriteLine($"There was an error when trying to create a global '{Constants.OptionsFile}' file in '{env.SharedDirectoryPath}'. {e.ToString()}");
            }
        }
    }
}