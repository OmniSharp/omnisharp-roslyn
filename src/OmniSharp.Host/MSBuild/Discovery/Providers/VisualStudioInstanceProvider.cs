using System;
using System.Collections.Immutable;
using System.IO;
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
            if (!PlatformHelper.IsWindows)
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

                    var instance = instances[0];
                    var state = ((ISetupInstance2)instance).GetState();

                    if (!Version.TryParse(instance.GetInstallationVersion(), out var version))
                    {
                        continue;
                    }

                    if (state == InstanceState.Complete)
                    {
                        // Note: The code below will likely fail if MSBuild's version increments.
                        var toolsPath = Path.Combine(instance.GetInstallationPath(), "MSBuild", "15.0", "Bin");
                        if (Directory.Exists(toolsPath))
                        {
                            builder.Add(
                                new MSBuildInstance(
                                    instance.GetDisplayName(),
                                    toolsPath,
                                    version,
                                    DiscoveryType.VisualStudioSetup));
                        }
                    }
                }
                while (fetched < 0);

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
