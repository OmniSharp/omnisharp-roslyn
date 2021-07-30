using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OmniSharp.Options;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Shared]
    [Export(typeof(IHostServicesProvider))]
    [Export(typeof(ExternalFeaturesHostServicesProvider))]
    public class ExternalFeaturesHostServicesProvider : IHostServicesProvider
    {
        public ImmutableArray<Assembly> Assemblies { get; }

        [ImportingConstructor]
        public ExternalFeaturesHostServicesProvider(IAssemblyLoader loader, OmniSharpOptions options, IOmniSharpEnvironment environment, ILoggerFactory loggerFactory)
        {
            var builder = ImmutableArray.CreateBuilder<Assembly>();

            var roslynExtensionsLocations = options.RoslynExtensionsOptions.GetNormalizedLocationPaths(environment);
            if (roslynExtensionsLocations?.Any() == true)
            {
                var logger = loggerFactory.CreateLogger<ExternalFeaturesHostServicesProvider>();
                foreach (var roslynExtensionsLocation in roslynExtensionsLocations)
                {
                    var loadedAssemblies = loader.LoadAllFrom(roslynExtensionsLocation);
                    if (loadedAssemblies.Any())
                    {
                        builder.AddRange(loadedAssemblies);
                    }
                    else
                    {
                        logger.LogWarning($"The path '{roslynExtensionsLocation}' is configured in the RoslynExtensionsOptions as the external features source but no assemblies were found at this path.");
                    }
                }
            }

            Assemblies = builder.ToImmutable();
        }
    }
}
