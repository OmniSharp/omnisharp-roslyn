using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Immutable;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class UserOverrideInstanceProvider : MSBuildInstanceProvider
    {
        private readonly MSBuildOverrideOptions _options;

        public UserOverrideInstanceProvider(ILoggerFactory loggerFactory, IConfiguration configuration)
            : base(loggerFactory)
        {
            _options = configuration.GetSection("msbuildoverride").Get<MSBuildOverrideOptions>();
        }

        public override ImmutableArray<MSBuildInstance> GetInstances()
        {
            if (_options == null || string.IsNullOrWhiteSpace(_options.MSBuildPath))
            {
                return ImmutableArray<MSBuildInstance>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<MSBuildInstance>();
            builder.Add(
                new MSBuildInstance(
                    _options.Name ?? $"Overridden MSBuild from {_options.MSBuildPath}",
                    _options.MSBuildPath,
                    new Version(99, 0),
                    DiscoveryType.UserOverride,
                    _options.PropertyOverrides?.ToImmutableDictionary()));
            return builder.ToImmutable();

        }
    }
}
