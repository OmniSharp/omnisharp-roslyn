using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicrosoftBuildLocator = Microsoft.Build.Locator.MSBuildLocator;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class SdkInstanceProvider : MSBuildInstanceProvider
    {
        private readonly SdkOptions _options;

        public SdkInstanceProvider(ILoggerFactory loggerFactory, IConfiguration sdkConfiguration)
            : base(loggerFactory)
        {
            _options = sdkConfiguration?.Get<SdkOptions>();
        }

        public override ImmutableArray<MSBuildInstance> GetInstances()
        {
            var includePrerelease = _options?.IncludePrereleases == true;

            SemanticVersion optionsVersion = null;
            if (!string.IsNullOrEmpty(_options?.Version) &&
                !TryParseVersion(_options.Version, out optionsVersion, out var errorMessage))
            {
                Logger.LogError(errorMessage);
                return NoInstances;
            }

            var instances = MicrosoftBuildLocator.QueryVisualStudioInstances()
                .Where(instance => IncludeSdkInstance(instance.VisualStudioRootPath, optionsVersion, includePrerelease))
                .OrderByDescending(instance => instance.Version)
                .ToImmutableArray();

            if (instances.Length == 0)
            {
                if (optionsVersion is null)
                {
                    Logger.LogError($"OmniSharp requires the .NET 6 SDK or higher be installed. Please visit https://dotnet.microsoft.com/download/dotnet/6.0 to download the .NET SDK.");
                }
                else
                {
                    Logger.LogError($"The Sdk version specified in the OmniSharp settings could not be found. Configured version is '{optionsVersion}'. Please update your settings and restart OmniSharp.");
                }

                return NoInstances;
            }

            return instances.Select(instance =>
            {
                var microsoftBuildPath = Path.Combine(instance.MSBuildPath, "Microsoft.Build.dll");
                var version = GetMSBuildVersion(microsoftBuildPath);

                return new MSBuildInstance(
                    $"{instance.Name} {instance.Version}",
                    instance.MSBuildPath,
                    version,
                    DiscoveryType.DotNetSdk,
                    _options?.PropertyOverrides?.ToImmutableDictionary());
            }).ToImmutableArray();
        }

        public static bool TryParseVersion(string versionString, out SemanticVersion version, out string errorMessage)
        {
            if (!SemanticVersion.TryParse(versionString, out version))
            {
                errorMessage = $"The Sdk version specified in the OmniSharp settings was not a valid semantic version. Configured version is '{versionString}'. Please update your settings and restart OmniSharp.";
                return false;
            }
            else if (version.Major < 6)
            {
                errorMessage = $"The Sdk version specified in the OmniSharp settings is not .NET 6 or higher. Configured version is '{versionString}'. Please update your settings and restart OmniSharp.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public static bool IncludeSdkInstance(string sdkPath, SemanticVersion targetVersion, bool includePrerelease)
        {
            // If the path does not have a `.version` file, then do not consider it a valid option.
            if (!TryGetSdkVersion(sdkPath, out var version))
            {
                return false;
            }

            // If the version is less than .NET 6, then do not consider it a valid option.
            if (version.Major < 6)
            {
                return false;
            }

            // If a target version was specified, then only a matching version is a valid option.
            if (targetVersion is not null)
            {
                return version.Equals(targetVersion);
            }

            // If we are including prereleases then everything else is valid, otherwise check that it is not a prerelease sdk.
            return includePrerelease ||
                string.IsNullOrEmpty(version.PreReleaseLabel);
        }

        public static bool TryGetSdkVersion(string sdkPath, out SemanticVersion version)
        {
            version = null;

            var versionPath = Path.Combine(sdkPath, ".version");
            if (!File.Exists(versionPath))
            {
                return false;
            }

            var lines = File.ReadAllLines(versionPath);
            foreach (var line in lines)
            {
                if (SemanticVersion.TryParse(line, out version))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
