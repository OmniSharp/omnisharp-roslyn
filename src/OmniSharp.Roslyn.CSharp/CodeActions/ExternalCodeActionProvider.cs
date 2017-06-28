using System.Composition;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    [Shared]
    [Export(typeof(ICodeActionProvider))]
    public class ExternalCodeActionProvider : AbstractCodeActionProvider
    {
        [ImportingConstructor]
        public ExternalCodeActionProvider(ExternalFeaturesHostServicesProvider hostServicesProvider)
            : base("ExternalCodeActions", hostServicesProvider.Assemblies)
        {
        }
    }
}
