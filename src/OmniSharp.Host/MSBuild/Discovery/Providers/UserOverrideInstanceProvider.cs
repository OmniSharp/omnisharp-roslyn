using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.IO;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class UserOverrideInstanceProvider : MSBuildInstanceProvider
    {
        private readonly MSBuildOverrideOptions _options;

        public UserOverrideInstanceProvider(ILoggerFactory loggerFactory, IConfiguration msbuildConfiguration)
            : base(loggerFactory)
        {
            _options = msbuildConfiguration?.GetSection("msbuildoverride").Get<MSBuildOverrideOptions>();
        }

        public override ImmutableArray<MSBuildInstance> GetInstances()
        {
            if (_options == null || string.IsNullOrWhiteSpace(_options.MSBuildPath))
            {
                return ImmutableArray<MSBuildInstance>.Empty;
            }

            var version = GetMSBuildVersion(Path.Combine(_options.MSBuildPath, "Microsoft.Build.dll"));

            var builder = ImmutableArray.CreateBuilder<MSBuildInstance>();
            builder.Add(
                new MSBuildInstance(
                    _options.Name ?? $"Overridden MSBuild from {_options.MSBuildPath}",
                    _options.MSBuildPath,
                    version,
                    DiscoveryType.UserOverride,
                    _options.PropertyOverrides?.ToImmutableDictionary()));
            return builder.ToImmutable();
        }
    }
}
