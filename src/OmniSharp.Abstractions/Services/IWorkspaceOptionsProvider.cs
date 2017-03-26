using Microsoft.CodeAnalysis.Options;

namespace OmniSharp.Services
{
    public interface IWorkspaceOptionsProvider
    {
        OptionSet Process(OptionSet optionSet, Options.FormattingOptions options);
    }
}