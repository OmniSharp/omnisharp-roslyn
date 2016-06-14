using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IHostServicesProvider))]
    [Export(typeof(RoslynFeaturesHostServicesProvider))]
    public class RoslynFeaturesHostServicesProvider : IHostServicesProvider
    {
        public ImmutableArray<Assembly> Assemblies { get; }

        [ImportingConstructor]
        public RoslynFeaturesHostServicesProvider(IOmnisharpAssemblyLoader loader)
        {
            var builder = ImmutableArray.CreateBuilder<Assembly>();

            var Features = Configuration.GetRoslynAssemblyFullName("Microsoft.CodeAnalysis.Features");
            var CSharpFeatures = Configuration.GetRoslynAssemblyFullName("Microsoft.CodeAnalysis.CSharp.Features");

            builder.AddRange(loader.Load(Features, CSharpFeatures));

            this.Assemblies = builder.ToImmutable();
        }
    }
}
