using System.Composition;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    [Export(typeof(ICodeActionProvider))]
    public class ExternalCodeActionProvider : AbstractCodeActionProvider
    {
        [ImportingConstructor]
        public ExternalCodeActionProvider(ExternalFeaturesHostServicesProvider featuresHostServicesProvider)
            : base("ExternalCodeActions", featuresHostServicesProvider.Assemblies)
        {
        }
    }
}
