#nullable enable

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Completion;
using Microsoft.CodeAnalysis.Options;
using OmniSharp.Options;
using OmniSharp.Roslyn.Options;

namespace OmniSharp.Roslyn.CSharp.Services.Completion
{
    [Export(typeof(IWorkspaceOptionsProvider)), Shared]
    public class CompletionOptionsProvider : IWorkspaceOptionsProvider
    {
        public int Order => 0;

        public OptionSet Process(OptionSet currentOptionSet, OmniSharpOptions omniSharpOptions, IOmniSharpEnvironment omnisharpEnvironment)
            => currentOptionSet.WithChangedOption(
                option: OmniSharpCompletionService.ShowItemsFromUnimportedNamespaces,
                language: LanguageNames.CSharp,
                value: omniSharpOptions.RoslynExtensionsOptions.EnableImportCompletion);
    }
}
