using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [Shared]
    [Export(typeof(CachingCodeFixProviderForProjects))]
    public class CachingCodeFixProviderForProjects
    {
        private readonly ConcurrentDictionary<ProjectId, IEnumerable<CodeFixProvider>> _cache = new ConcurrentDictionary<ProjectId, IEnumerable<CodeFixProvider>>();

        public IEnumerable<CodeFixProvider> GetAllCodeFixesForProject(ProjectId projectId)
        {
            if (_cache.ContainsKey(projectId))
                return _cache[projectId];
            return Enumerable.Empty<CodeFixProvider>();
        }

        public void LoadFrom(ProjectInfo project)
        {
            var codeFixes = project.AnalyzerReferences
                .OfType<AnalyzerFileReference>()
                .SelectMany(analyzerFileReference => analyzerFileReference.GetAssembly().DefinedTypes)
                .Where(x => x.IsSubclassOf(typeof(CodeFixProvider)))
                .Select(x =>
                {
                    var attribute = x.GetCustomAttribute<ExportCodeFixProviderAttribute>();

                    if (attribute?.Languages != null && attribute.Languages.Contains(project.Language))
                    {
                        return (CodeFixProvider)Activator.CreateInstance(x.AsType());
                    }

                    return null;
                })
                .Where(x => x != null)
                .ToImmutableArray();

            _cache.AddOrUpdate(project.Id, codeFixes, (_, __) => codeFixes);
        }
    }
}
