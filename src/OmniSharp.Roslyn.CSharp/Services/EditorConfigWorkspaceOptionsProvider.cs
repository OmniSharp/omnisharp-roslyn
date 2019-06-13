using System.Composition;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.VisualStudio.CodingConventions;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Formatting.EditorConfig;
using OmniSharp.Roslyn.Options;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IWorkspaceOptionsProvider)), Shared]
    public class EditorConfigWorkspaceOptionsProvider : IWorkspaceOptionsProvider
    {
        public int Order => -100;

        public OptionSet Process(OptionSet workOptionSet, FormattingOptions options, IOmniSharpEnvironment omnisharpEnvironment)
        {
            var changedOptionSet = workOptionSet.WithEditorConfigOptions(omnisharpEnvironment.TargetDirectory).GetAwaiter().GetResult();
            return changedOptionSet;
        }
    }
}
