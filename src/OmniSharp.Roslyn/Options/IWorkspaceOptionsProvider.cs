using Microsoft.CodeAnalysis.Options;
using OmniSharp.Options;

namespace OmniSharp.Roslyn.Options
{
    public interface IWorkspaceOptionsProvider
    {
        OptionSet Process(OptionSet currentOptionSet, OmniSharpOptions omniSharpOptions, IOmniSharpEnvironment omnisharpEnvironment);

        int Order { get; }
    }
}
