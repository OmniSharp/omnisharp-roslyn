#nullable enable

using System;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.RoslynInternals.Analyzers
{
    public static class RoslynAnalyzerAssemblyLoaderFactory
    {
        public static IAnalyzerAssemblyLoader CreateShadowCopyAnalyzerAssemblyLoader()
        {
            var type = RoslynReflection.GetType(RoslynReflection.CodeAnalysisAssembly, "Microsoft.CodeAnalysis.AnalyzerAssemblyLoader");
            var clean = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SingleOrDefault(m => m.Name == "CleanLegacyShadowCopyDirectoryIfNeeded" &&
                                      m.GetParameters().Length == 1);
            var create = RoslynReflection.GetMethod(type, "CreateNonLockingLoader",
                m => m.IsStatic && m.GetParameters().Length >= 1 &&
                     m.GetParameters().Skip(1).All(p => p.IsOptional));
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CodeAnalysis");
            if (clean is not null)
                RoslynReflection.Invoke(clean, null, System.IO.Path.Combine(root, "OmnisharpAnalyzerShadowCopies"));
            var arguments = Enumerable.Repeat<object>(Type.Missing, create.GetParameters().Length).ToArray();
            arguments[0] = System.IO.Path.Combine(root, "OmnisharpAnalyzerPathResolver");
            return RoslynReflection.Invoke<IAnalyzerAssemblyLoader>(create, null, arguments);
        }
    }

    public static class RoslynWorkspaceAnalyzerOptionsFactory
    {
        public static AnalyzerOptions Create(Solution solution, AnalyzerOptions options) => options;
    }
}
