using System.Composition;
using System.IO;
using Microsoft.CodeAnalysis.Options;
using Microsoft.Extensions.Logging;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Formatting.EditorConfig;
using OmniSharp.Roslyn.Options;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IWorkspaceOptionsProvider)), Shared]
    public class EditorConfigWorkspaceOptionsProvider : IWorkspaceOptionsProvider
    {
        private readonly ILoggerFactory _loggerFactory;

        public int Order => 200;

        [ImportingConstructor]
        public EditorConfigWorkspaceOptionsProvider(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public OptionSet Process(OptionSet currentOptionSet, OmniSharpOptions omnisharpOptions, IOmniSharpEnvironment omnisharpEnvironment)
        {
            if (!omnisharpOptions.FormattingOptions.EnableEditorConfigSupport) return currentOptionSet;

            // this is a dummy file that doesn't exist, but we simply want to tell .editorconfig to load *.cs specific settings
            var filePath = Path.Combine(omnisharpEnvironment.TargetDirectory, "omnisharp.cs");
            var changedOptionSet = currentOptionSet.WithEditorConfigOptions(filePath, _loggerFactory).GetAwaiter().GetResult();
            return changedOptionSet;
        }
    }
}
