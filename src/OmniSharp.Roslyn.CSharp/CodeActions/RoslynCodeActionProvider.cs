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
            ImmutableArray.Create(
                Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.CSharp.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")),
                Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"))
            );

        // TODO: Come in and pass Microsoft.CodeAnalysis.Features as well (today this breaks)
        public RoslynCodeActionProvider() :
            base("Roslyn", _mefAssemblies[0])
        {
        }
    }
}
