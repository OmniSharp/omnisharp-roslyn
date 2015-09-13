#if DNX451
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    [Export(typeof(ICodeActionProvider))]
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
    }
}
#endif
