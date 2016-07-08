using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using OmniSharp.Services;

namespace OmniSharp
{
    [Export]
    public class HostServicesAggregator
    {
        private readonly ImmutableArray<Assembly> _assemblies;

        [ImportingConstructor]
        public HostServicesAggregator(
            [ImportMany] IEnumerable<IHostServicesProvider> hostServicesProviders)
        {
            var builder = ImmutableHashSet.CreateBuilder<Assembly>();

            // We always include the default Roslyn assemblies, which includes:
            //
            //   * Microsoft.CodeAnalysis.Workspaces
            //   * Microsoft.CodeAnalysis.CSharp.Workspaces
            //   * Microsoft.CodeAnalysis.VisualBasic.Workspaces

            foreach (var assembly in MefHostServices.DefaultAssemblies)
            {
                builder.Add(assembly);
            }

            foreach (var provider in hostServicesProviders)
            {
                foreach (var assembly in provider.Assemblies)
                {
                    builder.Add(assembly);
                }
            }

            _assemblies = builder.ToImmutableArray();
        }

        public HostServices CreateHostServices()
        {
            return MefHostServices.Create(_assemblies);
        }
    }
}
