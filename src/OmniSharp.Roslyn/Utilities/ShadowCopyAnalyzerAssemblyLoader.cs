using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Analyzers;

namespace OmniSharp.Roslyn.Utilities
{
    public static class ShadowCopyAnalyzerAssemblyLoader
    {
        public static readonly IAnalyzerAssemblyLoader Instance = OmnisharpAnalyzerAssemblyLoaderFactory.CreateShadowCopyAnalyzerAssemblyLoader();
    }
}
