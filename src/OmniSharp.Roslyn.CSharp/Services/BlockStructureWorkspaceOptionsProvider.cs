using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Structure;
using Microsoft.CodeAnalysis.Options;
using OmniSharp.Options;
using OmniSharp.Roslyn.Options;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IWorkspaceOptionsProvider)), Shared]
    public class BlockStructureWorkspaceOptionsProvider : IWorkspaceOptionsProvider
    {
        [ImportingConstructor]
        public BlockStructureWorkspaceOptionsProvider()
        {
        }

        public int Order => 140;

        public OptionSet Process(OptionSet currentOptionSet, OmniSharpOptions omniSharpOptions, IOmniSharpEnvironment omnisharpEnvironment)
        {
            return currentOptionSet
                .WithChangedOption(
                    OmniSharpBlockStructureOptions.ShowBlockStructureGuidesForCommentsAndPreprocessorRegions,
                    LanguageNames.CSharp,
                    true)
                .WithChangedOption(
                    OmniSharpBlockStructureOptions.ShowOutliningForCommentsAndPreprocessorRegions,
                    LanguageNames.CSharp,
                    true);
        }
    }
}
