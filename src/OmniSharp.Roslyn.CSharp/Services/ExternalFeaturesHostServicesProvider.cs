using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
using OmniSharp.Options;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IHostServicesProvider))]
    [Export(typeof(ExternalFeaturesHostServicesProvider))]
    [Shared]
    public class ExternalFeaturesHostServicesProvider : IHostServicesProvider
    {
        public ImmutableArray<Assembly> Assemblies { get; }

        [ImportingConstructor]
        public ExternalFeaturesHostServicesProvider(IAssemblyLoader loader, OmniSharpOptions options, IOmniSharpEnvironment env)
        {
            var builder = ImmutableArray.CreateBuilder<Assembly>();

            var roslynExtensionsLocations = options.RoslynExtensionsOptions.GetNormalizedLocationPaths(env);
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
