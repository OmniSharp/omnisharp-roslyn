using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
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
        public ExternalFeaturesHostServicesProvider(IAssemblyLoader loader, OmniSharpOptions options, IOmniSharpEnvironment environment)
        {
            var builder = ImmutableArray.CreateBuilder<Assembly>();

            var roslynExtensionsLocations = options.RoslynExtensionsOptions.GetNormalizedLocationPaths(environment);
            if (roslynExtensionsLocations?.Any() == true)
            {
                foreach (var roslynExtensionsLocation in roslynExtensionsLocations)
                {
                    builder.AddRange(loader.LoadAllFrom(roslynExtensionsLocation));
                }
            }

            Assemblies = builder.ToImmutable();
        }
    }
}
