using System;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace OmniSharp.MSBuild.ProjectFile
{
    public class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        private readonly ConcurrentDictionary<string, Assembly> _assemblyPaths = new ConcurrentDictionary<string, Assembly>();

        public void AddDependencyLocation(string fullPath)
        {
            if(!_assemblyPaths.ContainsKey(fullPath))
                _assemblyPaths.TryAdd(fullPath, Assembly.LoadFrom(fullPath));
        }

        public Assembly LoadFromPath(string fullPath)
        {
            if (!_assemblyPaths.ContainsKey(fullPath))
                throw new InvalidOperationException($"Could not find analyzer reference '{fullPath}' from cache.");

            return _assemblyPaths[fullPath];
        }
    }
}
