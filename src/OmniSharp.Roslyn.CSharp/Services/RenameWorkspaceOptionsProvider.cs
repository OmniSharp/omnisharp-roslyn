using System.Composition;
using Microsoft.CodeAnalysis.Options;
using OmniSharp.Options;
using OmniSharp.Roslyn.Options;
using RoslynRenameOptions = Microsoft.CodeAnalysis.Rename.RenameOptions;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IWorkspaceOptionsProvider)), Shared]
    public class RenameWorkspaceOptionsProvider : IWorkspaceOptionsProvider
    {
        public int Order => 100;

        public OptionSet Process(OptionSet currentOptionSet, OmniSharpOptions omniSharpOptions, IOmniSharpEnvironment omnisharpEnvironment) =>
            currentOptionSet
               .WithChangedOption(RoslynRenameOptions.RenameInComments, omniSharpOptions.RenameOptions.RenameInComments)
               .WithChangedOption(RoslynRenameOptions.RenameInStrings, omniSharpOptions.RenameOptions.RenameInStrings)
               .WithChangedOption(RoslynRenameOptions.RenameOverloads, omniSharpOptions.RenameOptions.RenameOverloads);
    }
}
