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
    public class ImplementTypeWorkspaceOptionsProvider : IWorkspaceOptionsProvider
    {
        private readonly IAssemblyLoader _assemblyLoader;
        private readonly Lazy<Assembly> _csharpFeatureAssembly;
        private readonly Lazy<Type> _implementTypeOptions;

        [ImportingConstructor]
        public ImplementTypeWorkspaceOptionsProvider(IAssemblyLoader assemblyLoader)
        {
            _assemblyLoader = assemblyLoader;
            _csharpFeatureAssembly = _assemblyLoader.LazyLoad(Configuration.RoslynFeatures);
            _implementTypeOptions = _csharpFeatureAssembly.LazyGetType("Microsoft.CodeAnalysis.ImplementType.ImplementTypeOptions");
        }

        public int Order => 110;

        public OptionSet Process(OptionSet currentOptionSet, OmniSharpOptions omniSharpOptions, IOmniSharpEnvironment omnisharpEnvironment)
        {
            if (omniSharpOptions.ImplementTypeOptions.InsertionBehavior != null)
            {
                if (_implementTypeOptions.Value.GetField("InsertionBehavior", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is IOption insertionBehaviorOptionValue)
                {
                    currentOptionSet = currentOptionSet.WithChangedOption(new OptionKey(insertionBehaviorOptionValue, LanguageNames.CSharp), (int)omniSharpOptions.ImplementTypeOptions.InsertionBehavior);
                }
            }

            if (omniSharpOptions.ImplementTypeOptions.PropertyGenerationBehavior != null)
            {
                if (_implementTypeOptions.Value.GetField("PropertyGenerationBehavior", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is IOption propertyGenerationBehaviorOptionValue)
                {
                    currentOptionSet = currentOptionSet.WithChangedOption(new OptionKey(propertyGenerationBehaviorOptionValue, LanguageNames.CSharp), (int)omniSharpOptions.ImplementTypeOptions.PropertyGenerationBehavior);
                }
            }

            return currentOptionSet;
        }
    }
}
