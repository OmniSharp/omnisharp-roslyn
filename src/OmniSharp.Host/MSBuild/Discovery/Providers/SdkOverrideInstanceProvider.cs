using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class SdkOverrideInstanceProvider : MSBuildInstanceProvider
    {
        private readonly SdkOptions _options;

        public SdkOverrideInstanceProvider(ILoggerFactory loggerFactory, IConfiguration sdkConfiguration)
            : base(loggerFactory)
        {
            _options = sdkConfiguration?.Get<SdkOptions>();
        }

        public override ImmutableArray<MSBuildInstance> GetInstances()
        {
            if (string.IsNullOrEmpty(_options?.Path))
            {
                return NoInstances;
            }

            if (!File.Exists(_options.Path))
            {
                Logger.LogError($"The Sdk path specified in the OmniSharp settings does not exist. Configured path is '{_options.Path}'. Please update your settings and restart OmniSharp.");
                return NoInstances;
            }

            if (!SdkInstanceProvider.TryGetSdkVersion(_options.Path, out var pathVersion))
            {
                Logger.LogError($"The Sdk path specified in the OmniSharp settings does not appear to be a valid SDK. Configured path is '{_options.Path}'. Please update your settings and restart OmniSharp.");
                return NoInstances;
            }

            if (pathVersion.Major < 6)
            {
                Logger.LogError($"The Sdk path specified in the OmniSharp settings is not .NET 6 or higher. Reported version is '{pathVersion}'. Please update your settings and restart OmniSharp.");
                return NoInstances;
            }

            return ImmutableArray.Create(new MSBuildInstance(
                $".NET SDK {pathVersion} from {_options.Path}",
                _options.Path,
                new Version(pathVersion.Major, pathVersion.Minor, pathVersion.Patch),
                DiscoveryType.UserOverride,
                _options?.PropertyOverrides?.ToImmutableDictionary()));
        }
    }
}
