using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using OmniSharp.Options;
using OmniSharp.Services;
using System.Linq;

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

            var codeActionLocations = options.CodeActions.GetNormalizedLocationPaths(env);
            if (codeActionLocations?.Any() == true)
            {
                foreach (var codeActionLocation in codeActionLocations)
                {
                    builder.AddRange(loader.LoadAllFrom(codeActionLocation));
                }
            }

            Assemblies = builder.ToImmutable();
        }
    }
}
