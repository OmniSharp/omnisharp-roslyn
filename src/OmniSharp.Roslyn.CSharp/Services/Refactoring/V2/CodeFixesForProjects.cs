using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [Shared]
    [Export(typeof(CachingCodeFixProviderForProjects))]
    public class CachingCodeFixProviderForProjects
    {
        private readonly ConcurrentDictionary<string, IEnumerable<CodeFixProvider>> _cache = new ConcurrentDictionary<string, IEnumerable<CodeFixProvider>>();
        private readonly IAssemblyLoader _assemblyLoader;

        [ImportingConstructor]
        public CachingCodeFixProviderForProjects(IAssemblyLoader assemblyLoader)
        {
            _assemblyLoader = assemblyLoader;
        }

        public IEnumerable<CodeFixProvider> GetAllCodeFixesForProject(string projectId)
        {
            if (_cache.ContainsKey(projectId))
                return _cache[projectId];
            return Enumerable.Empty<CodeFixProvider>();
        }

        public void LoadFrom(string projectId, IEnumerable<string> AnalyzerPaths)
        {
            var codeFixes = AnalyzerPaths
                .Where(x => x.EndsWith("CodeFixes.dll"))
                .SelectMany(codeFixDllPath =>
                {
                    var loadedAssembly = _assemblyLoader.LoadFrom(codeFixDllPath);
                    var validTypes = loadedAssembly.GetTypes()
                        .Where(type => !type.GetTypeInfo().IsInterface && !type.GetTypeInfo().IsAbstract && !type.GetTypeInfo().ContainsGenericParameters)
                        .Where(t => typeof(CodeFixProvider).IsAssignableFrom(t));

                    return validTypes
                        .Select(type => type.CreateInstance<CodeFixProvider>())
                        .Where(instance => instance != null);
                });

            _cache.AddOrUpdate(projectId, codeFixes, (_, __) => codeFixes);
        }
    }
}
