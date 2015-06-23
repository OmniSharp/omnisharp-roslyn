#if DNX451
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace OmniSharp.Services
{
    public class RoslynCodeActionProvider : AbstractCodeActionProvider
    {
        private static ImmutableArray<Assembly> _mefAssemblies =>
            ImmutableArray.Create<Assembly>(
                Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"),
                Assembly.Load("Microsoft.CodeAnalysis.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")
            );

        public RoslynCodeActionProvider() : base(_mefAssemblies[0])
        {
        }

        public override string ProviderName => "Roslyn";
        internal static ImmutableArray<Assembly> MefAssemblies => _mefAssemblies;
    }
}
#endif
