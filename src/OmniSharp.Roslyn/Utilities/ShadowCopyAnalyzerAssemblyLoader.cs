using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Analyzers;

namespace OmniSharp.Roslyn.Utilities
{
    public static class ShadowCopyAnalyzerAssemblyLoader
    {
        public static readonly IAnalyzerAssemblyLoader Instance = CreateShadowCopyLoader();

        public static IAnalyzerAssemblyLoader CreateShadowCopyLoader()
            => OperatingSystem.IsWindows()
                ? OmnisharpAnalyzerAssemblyLoaderFactory.CreateShadowCopyAnalyzerAssemblyLoader()
                : new DefaultAnalyzerAssemblyLoader();

        private sealed class DefaultAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
        {
            private readonly ConcurrentDictionary<string, string> _dependencyPaths = new(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, Assembly> _assemblies = new(StringComparer.OrdinalIgnoreCase);

            public DefaultAnalyzerAssemblyLoader()
            {
                AssemblyLoadContext.Default.Resolving += ResolveAssembly;
            }

            public void AddDependencyLocation(string fullPath)
            {
                if (!Path.IsPathFullyQualified(fullPath))
                {
                    throw new ArgumentException("The path must be absolute.", nameof(fullPath));
                }

                _dependencyPaths[AssemblyName.GetAssemblyName(fullPath).Name] = fullPath;
            }

            public Assembly LoadFromPath(string fullPath)
            {
                if (!Path.IsPathFullyQualified(fullPath))
                {
                    throw new ArgumentException("The path must be absolute.", nameof(fullPath));
                }

                return _assemblies.GetOrAdd(fullPath, AssemblyLoadContext.Default.LoadFromAssemblyPath);
            }

            private Assembly ResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
                => assemblyName.Name != null && _dependencyPaths.TryGetValue(assemblyName.Name, out var path)
                    ? LoadFromPath(path)
                    : null;
        }
    }
}
