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
            if (env?.SharedPath == null) return;

            try
            {
                if (!Directory.Exists(env.SharedPath))
                {
                    Directory.CreateDirectory(env.SharedPath);
                }

                var omnisharpGlobalFilePath = Path.Combine(env.SharedPath, OmniSharpConstants.OmnisharpOptionsFile);
                if (!File.Exists(omnisharpGlobalFilePath))
                {
                    File.WriteAllText(omnisharpGlobalFilePath, "{}");
                }

                configBuilder.AddJsonFile(
                    new PhysicalFileProvider(env.SharedPath),
                    OmniSharpConstants.OmnisharpOptionsFile,
                    optional: true,
                    reloadOnChange: true);
            }
            catch (Exception e)
            {
                // at this point we have no ILogger yet
                Console.Error.WriteLine($"There was an error when trying to create a global '{OmniSharpConstants.OmnisharpOptionsFile}' file in '{env.SharedPath}'. {e.ToString()}");
            }
        }
    }
}