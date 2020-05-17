using System;
using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using OmniSharp.Options;
using OmniSharp.Roslyn.Options;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IWorkspaceOptionsProvider)), Shared]
    public class BlockStructureWorkspaceOptionsProvider : IWorkspaceOptionsProvider
    {
        private readonly IAssemblyLoader _assemblyLoader;
        private readonly Lazy<Assembly> _csharpFeatureAssembly;
        private readonly Lazy<Type> _blockStructureOptions;

        [ImportingConstructor]
        public BlockStructureWorkspaceOptionsProvider(IAssemblyLoader assemblyLoader)
        {
            _assemblyLoader = assemblyLoader;
            _csharpFeatureAssembly = _assemblyLoader.LazyLoad(Configuration.RoslynFeatures);
            _blockStructureOptions = _csharpFeatureAssembly.LazyGetType("Microsoft.CodeAnalysis.Structure.BlockStructureOptions");
        }

        public int Order => 140;

        public OptionSet Process(OptionSet currentOptionSet, OmniSharpOptions omniSharpOptions, IOmniSharpEnvironment omnisharpEnvironment)
        {
            if (_blockStructureOptions.Value.GetField("ShowBlockStructureGuidesForCommentsAndPreprocessorRegions", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is IOption showBlockStructureGuidesForCommentsAndPreprocessorRegionsOptionValue)
            {
                currentOptionSet = currentOptionSet.WithChangedOption(new OptionKey(showBlockStructureGuidesForCommentsAndPreprocessorRegionsOptionValue, LanguageNames.CSharp), true);
            }

            if (_blockStructureOptions.Value.GetField("ShowOutliningForCommentsAndPreprocessorRegions", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is IOption showOutliningForCommentsAndPreprocessorRegionsOptionValue)
            {
                currentOptionSet = currentOptionSet.WithChangedOption(new OptionKey(showOutliningForCommentsAndPreprocessorRegionsOptionValue, LanguageNames.CSharp), true);
            }

            return currentOptionSet;
        }
    }
}
