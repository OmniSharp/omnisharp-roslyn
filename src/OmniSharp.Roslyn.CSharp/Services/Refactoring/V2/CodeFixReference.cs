using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [Shared]
    [Export(typeof(CodeFixesForProjects))]
    public class CodeFixesForProjects
    {
        private readonly ConcurrentDictionary<string, IEnumerable<CodeFixProvider>> codeFixCache = new ConcurrentDictionary<string, IEnumerable<CodeFixProvider>>();

        [ImportingConstructor]
        public CodeFixesForProjects()
        {
        }

        public IEnumerable<CodeFixProvider> GetAllCodeFixesForProject(string projectId)
        {
            if (codeFixCache.ContainsKey(projectId))
                return codeFixCache[projectId];
            return Enumerable.Empty<CodeFixProvider>();
        }

        public void LoadFrom(string projectId, IEnumerable<string> AnalyzerPaths)
        {
            var codeFixes = AnalyzerPaths
                .Where(x => x.EndsWith("CodeFixes.dll"))
                .SelectMany(codeFixDllPath =>
                {
                    var loadedAssembly = Assembly.LoadFrom(codeFixDllPath);
                    var validTypes = loadedAssembly.GetTypes()
                        .Where(type => !type.GetTypeInfo().IsInterface && !type.GetTypeInfo().IsAbstract && !type.GetTypeInfo().ContainsGenericParameters)
                        .Where(t => typeof(CodeFixProvider).IsAssignableFrom(t));

                    return validTypes
                        .Select(type => CreateInstance<CodeFixProvider>(type))
                        .Where(instance => instance != null);
                });

            codeFixCache.AddOrUpdate(projectId, codeFixes, (_, __) => codeFixes);
        }

        private static T CreateInstance<T>(Type type) where T : class
        {
            try
            {
                var defaultCtor = type.GetConstructor(new Type[] { });

                return defaultCtor != null
                    ? (T)Activator.CreateInstance(type)
                    : null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create instrance of {type.FullName} in {type.AssemblyQualifiedName}.", ex);
            }
        }
    }
}
