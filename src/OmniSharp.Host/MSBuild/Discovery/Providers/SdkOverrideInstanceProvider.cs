using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
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

            if (!TryGetVersion(_options.Path, out var pathVersion, out var errorMessage))
            {
                Logger.LogError(errorMessage);
                return NoInstances;
            }

            return ImmutableArray.Create(new MSBuildInstance(
                $".NET SDK {pathVersion} from {_options.Path}",
                _options.Path,
                new Version(pathVersion.Major, pathVersion.Minor, pathVersion.Patch),
                DiscoveryType.UserOverride,
                _options?.PropertyOverrides?.ToImmutableDictionary()));
        }

        public static bool TryGetVersion(string path, out SemanticVersion version, out string errorMessage)
        {
            if (!Directory.Exists(path))
            {
                version = null;
                errorMessage = $"The Sdk path specified in the OmniSharp settings does not exist. Configured path is '{path}'. Please update your settings and restart OmniSharp.";
                return false;
            }

            if (!SdkInstanceProvider.TryGetSdkVersion(path, out version))
            {
                errorMessage = $"The Sdk path specified in the OmniSharp settings does not appear to be a valid SDK. Configured path is '{path}'. Please update your settings and restart OmniSharp.";
                return false;
            }

            if (version.Major < 6)
            {
                errorMessage = $"The Sdk path specified in the OmniSharp settings is not .NET 6 or higher. Reported version is '{version}'. Please update your settings and restart OmniSharp.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
