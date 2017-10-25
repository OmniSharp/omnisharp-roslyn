using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class MonoInstanceProvider : MSBuildInstanceProvider
    {
        public MonoInstanceProvider(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override ImmutableArray<MSBuildInstance> GetInstances()
        {
            if (!PlatformHelper.IsMono)
            {
                return ImmutableArray<MSBuildInstance>.Empty;
            }

            var path = PlatformHelper.GetMonoMSBuildDirPath();
            if (path == null)
            {
                return ImmutableArray<MSBuildInstance>.Empty;
            }

            var toolsPath = Path.Combine(path, "15.0", "bin");
            if (!Directory.Exists(toolsPath))
            {
                return ImmutableArray<MSBuildInstance>.Empty;
            }

            var propertyOverrides = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);

            var roslynPath = Path.Combine(toolsPath, "Roslyn");
            if (Directory.Exists(roslynPath))
            {
                propertyOverrides.Add("CscToolPath", roslynPath);
                propertyOverrides.Add("CscToolExe", "csc.exe");
            }

            return ImmutableArray.Create(
                new MSBuildInstance(
                    "Mono",
                    toolsPath,
                    new Version(15, 0),
                    DiscoveryType.Mono,
                    propertyOverrides.ToImmutable()));
        }
    }
}
