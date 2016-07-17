using System.Composition;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    [Export(typeof(ICodeActionProvider))]
    public class RoslynCodeActionProvider : AbstractCodeActionProvider
    {
        [ImportingConstructor]
        public RoslynCodeActionProvider(ILoggerFactory loggerFactory, RoslynFeaturesHostServicesProvider featuresHostServicesProvider)
            : base(loggerFactory, nameof(RoslynCodeActionProvider), featuresHostServicesProvider.Assemblies)
        {
        }
    }
}
