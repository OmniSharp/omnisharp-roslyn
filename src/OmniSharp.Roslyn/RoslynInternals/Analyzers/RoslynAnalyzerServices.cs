#nullable enable

using System;
using System.IO;
using System.Linq;
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
            if (!OperatingSystem.IsWindows())
            {
                return (IAnalyzerAssemblyLoader)Activator.CreateInstance(type, nonPublic: true)!;
            }

            var clean = RoslynReflection.GetMethod(
                type,
                "CleanLegacyShadowCopyDirectoryIfNeeded",
                parameterCount: 1,
                isStatic: true);
            var create = RoslynReflection.GetMethod(type, "CreateNonLockingLoader",
                m => m.IsStatic &&
                     m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string) &&
                     m.GetParameters().Skip(1).All(p => p.IsOptional));

            var root = Path.Combine(Path.GetTempPath(), "CodeAnalysis");
            RoslynReflection.Invoke(clean, null, Path.Combine(root, "OmnisharpAnalyzerShadowCopies"));

            var parameters = create.GetParameters();
            var arguments = parameters
                .Select(parameter => parameter.ParameterType.IsValueType
                    ? Activator.CreateInstance(parameter.ParameterType)
                    : null)
                .ToArray();
            arguments[0] = Path.Combine(root, "OmnisharpAnalyzerPathResolver");

            return RoslynReflection.Invoke<IAnalyzerAssemblyLoader>(create, null, arguments);
        }
    }

    public static class RoslynWorkspaceAnalyzerOptionsFactory
    {
        public static AnalyzerOptions Create(Solution solution, AnalyzerOptions options) => options;
    }
}
