using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using OmniSharp.Options;
using OmniSharp.Services;
using System.Linq;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IHostServicesProvider))]
    [Export(typeof(RoslynFeaturesHostServicesProvider))]
    public class RoslynFeaturesHostServicesProvider : IHostServicesProvider
    {
        public ImmutableArray<Assembly> Assemblies { get; }

        [ImportingConstructor]
        public RoslynFeaturesHostServicesProvider(IAssemblyLoader loader, OmniSharpOptions options, IOmniSharpEnvironment env)
        {
            var builder = ImmutableArray.CreateBuilder<Assembly>();

            var Features = Configuration.GetRoslynAssemblyFullName("Microsoft.CodeAnalysis.Features");
            var CSharpFeatures = Configuration.GetRoslynAssemblyFullName("Microsoft.CodeAnalysis.CSharp.Features");

            builder.AddRange(loader.Load(Features, CSharpFeatures));

            var codeActionLocations = options?.CodeActions.GetLocations(env);
            if (codeActionLocations != null && codeActionLocations.Any())
            {
                foreach (var codeActionLocation in codeActionLocations)
                {
                    builder.AddRange(loader.LoadAll(codeActionLocation));
                }
            }

            this.Assemblies = builder.ToImmutable();
        }
    }
}
