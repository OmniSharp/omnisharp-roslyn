using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Setup.Configuration;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class VisualStudioInstanceProvider : MSBuildInstanceProvider
    {
        public VisualStudioInstanceProvider(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override ImmutableArray<MSBuildInstance> GetInstances()
        {
            if (PlatformHelper.IsMono)
            {
                return NoInstances;
            }

            try
            {
                var configuration = Interop.GetSetupConfiguration();
                if (configuration == null)
                {
                    return NoInstances;
                }

                var builder = ImmutableArray.CreateBuilder<MSBuildInstance>();

                var instanceEnum = configuration.EnumAllInstances();

                int fetched;
                var instances = new ISetupInstance[1];
                do
                {
                    instanceEnum.Next(1, instances, out fetched);
                    if (fetched <= 0)
                    {
                        continue;
                    }

                    var instance = (ISetupInstance2)instances[0];
                    var state = instance.GetState();

                    var installVersion = instance.GetInstallationVersion();
                    var installPath = instance.GetInstallationPath();

                    if (!Version.TryParse(installVersion, out var version))
                    {
                        Logger.LogDebug($"Found Visual Studio installation with strange version number: {installVersion} ({installPath})");
                        continue;
                    }

                    if (state != InstanceState.Complete)
                    {
                        Logger.LogDebug($"Found incomplete Visual Studio installation ({installPath})");
                        continue;
                    }

                    if (!instance.GetPackages().Any(package => package.GetId() == "Microsoft.VisualStudio.Component.Roslyn.Compiler"))
                    {
                        Logger.LogDebug($"Found Visual Studio installation with no C# package installed ({installPath})");
                        continue;
                    }

                    var msbuildPath = Path.Combine(installPath, "MSBuild");

                    var toolsPath = FindMSBuildToolsPath(msbuildPath);
                    if (toolsPath != null)
                    {
                        builder.Add(
                            new MSBuildInstance(
                                instance.GetDisplayName(),
                                toolsPath,
                                version,
                                DiscoveryType.VisualStudioSetup));
                    }
                }
                while (fetched > 0);

                return builder.ToImmutable();
            }
            catch (COMException ex)
            {
                return LogExceptionAndReturnEmpty(ex);
            }
            catch (DllNotFoundException ex)
            {
                // This is OK, since it probably means that VS 2017 or later isn't installed.
                // We'll log the exception for debugging though.
                return LogExceptionAndReturnEmpty(ex);
            }
        }

        private ImmutableArray<MSBuildInstance> LogExceptionAndReturnEmpty(Exception ex)
        {
            Logger.LogDebug(ex, "An exception was thrown while retrieving Visual Studio instances.");

            return NoInstances;
        }
    }
}
