using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Shared]
    [Export(typeof(IHostServicesProvider))]
    [Export(typeof(RoslynFeaturesHostServicesProvider))]
    public class RoslynFeaturesHostServicesProvider : IHostServicesProvider
    {
        public ImmutableArray<Assembly> Assemblies { get; }

        [ImportingConstructor]
        public RoslynFeaturesHostServicesProvider(IAssemblyLoader loader)
        {
            var builder = ImmutableArray.CreateBuilder<Assembly>();
            builder.AddRange(loader.Load(Configuration.RoslynFeatures, Configuration.RoslynCSharpFeatures, Configuration.RoslynOmniSharpExternalAccess, Configuration.RoslynOmniSharpExternalAccessCSharp));
            Assemblies = builder.ToImmutable();
        }
    }
}
