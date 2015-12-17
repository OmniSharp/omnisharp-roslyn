using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.PlatformAbstractions;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    [Export(typeof(ICodeActionProvider))]
    public class RoslynCodeActionProvider : AbstractCodeActionProvider
    {
        private readonly static string[] _assembliesToLoad = new[]
        {
            "Microsoft.CodeAnalysis.CSharp.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
            "Microsoft.CodeAnalysis.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"
        };

        private static ImmutableArray<Assembly> _assemblies;

        private static ImmutableArray<Assembly> MefAssemblies
        {
            get
            {
                if (_assemblies == null)
                {
                    Func<string, Assembly> loader;
                    if (PlatformServices.Default.AssemblyLoadContextAccessor != null)
                    {
                        loader = name => PlatformServices.Default.AssemblyLoadContextAccessor.Default.Load(name);
                    }
                    else
                    {
                        loader = name => Assembly.Load(new AssemblyName(name));
                    }

                    _assemblies = _assembliesToLoad.Select(loader).ToImmutableArray();
                }

                return _assemblies;
            }
        }

        // TODO: Come in and pass Microsoft.CodeAnalysis.Features as well (today this breaks)
        public RoslynCodeActionProvider() : base(MefAssemblies[0])
        {
            base.Assemblies = MefAssemblies;
        }

        public override string ProviderName => "Roslyn";
    }
}
