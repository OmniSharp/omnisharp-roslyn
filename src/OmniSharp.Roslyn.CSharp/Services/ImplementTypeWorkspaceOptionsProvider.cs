using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.ImplementType;
using Microsoft.CodeAnalysis.Options;
using OmniSharp.Options;
using OmniSharp.Roslyn.Options;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IWorkspaceOptionsProvider)), Shared]
    public class ImplementTypeWorkspaceOptionsProvider : IWorkspaceOptionsProvider
    {
        [ImportingConstructor]
        public ImplementTypeWorkspaceOptionsProvider()
        {
        }

        public int Order => 110;

        public OptionSet Process(OptionSet currentOptionSet, OmniSharpOptions omniSharpOptions, IOmniSharpEnvironment omnisharpEnvironment)
        {
            if (omniSharpOptions.ImplementTypeOptions.InsertionBehavior != null)
            {
                currentOptionSet = OmniSharpImplementTypeOptions.SetInsertionBehavior(currentOptionSet, LanguageNames.CSharp, (OmniSharpImplementTypeInsertionBehavior)omniSharpOptions.ImplementTypeOptions.InsertionBehavior);
            }

            if (omniSharpOptions.ImplementTypeOptions.PropertyGenerationBehavior != null)
            {
                currentOptionSet = OmniSharpImplementTypeOptions.SetPropertyGenerationBehavior(currentOptionSet, LanguageNames.CSharp, (OmniSharpImplementTypePropertyGenerationBehavior)omniSharpOptions.ImplementTypeOptions.PropertyGenerationBehavior);
            }

            return currentOptionSet;
        }
    }
}
