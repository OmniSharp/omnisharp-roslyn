using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.RoslynInternals.Analyzers;

namespace OmniSharp.Roslyn.Utilities
{
    public static class ShadowCopyAnalyzerAssemblyLoader
    {
        public static readonly IAnalyzerAssemblyLoader Instance = CreateShadowCopyLoader();

        public static IAnalyzerAssemblyLoader CreateShadowCopyLoader() => RoslynAnalyzerAssemblyLoaderFactory.CreateShadowCopyAnalyzerAssemblyLoader();
    }
}
