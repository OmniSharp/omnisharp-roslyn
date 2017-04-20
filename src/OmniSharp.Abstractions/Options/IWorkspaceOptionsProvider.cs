using Microsoft.CodeAnalysis.Options;
using OmniSharp.Options;

namespace OmniSharp.Options
{
    public interface IWorkspaceOptionsProvider
    {
        OptionSet Process(OptionSet optionSet, FormattingOptions options);
    }
}