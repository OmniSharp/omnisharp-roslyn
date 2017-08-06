using System.Composition;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    [Shared]
    [Export(typeof(ICodeActionProvider))]
    public class RoslynCodeActionProvider : AbstractCodeActionProvider
    {
        [ImportingConstructor]
        public RoslynCodeActionProvider(RoslynFeaturesHostServicesProvider featuresHostServicesProvider)
            : base("Roslyn", featuresHostServicesProvider.Assemblies)
        {
        }
    }
}
