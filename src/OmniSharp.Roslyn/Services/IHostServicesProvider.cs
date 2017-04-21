using System.Collections.Immutable;
using System.Reflection;

namespace OmniSharp.Services
{
    public interface IHostServicesProvider
    {
        ImmutableArray<Assembly> Assemblies { get; }
    }
}
