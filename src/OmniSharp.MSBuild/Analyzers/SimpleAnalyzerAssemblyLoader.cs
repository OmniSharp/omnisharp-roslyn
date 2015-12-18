#if DNX451
using System;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace OmniSharp.MSBuild.Analyzers
{
    public class SimpleAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
            throw new NotImplementedException();
        }

        public Assembly LoadFromPath(string fullPath)
        {
            return Assembly.LoadFrom(fullPath);
        }
    }
}
#endif