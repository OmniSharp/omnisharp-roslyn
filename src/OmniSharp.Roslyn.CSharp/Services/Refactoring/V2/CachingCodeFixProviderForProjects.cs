﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [Shared]
    [Export(typeof(CachingCodeFixProviderForProjects))]
    public class CachingCodeFixProviderForProjects
    {
        private readonly ConcurrentDictionary<ProjectId, ImmutableArray<CodeFixProvider>> _cache = new ConcurrentDictionary<ProjectId, ImmutableArray<CodeFixProvider>>();
        private readonly ILogger<CachingCodeFixProviderForProjects> _logger;
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _providers;

        [ImportingConstructor]
        public CachingCodeFixProviderForProjects(ILoggerFactory loggerFactory, OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers)
        {
            _logger = loggerFactory.CreateLogger<CachingCodeFixProviderForProjects>();
            _workspace = workspace;
            _providers = providers;
            _workspace.WorkspaceChanged += (__, workspaceEvent) =>
            {
                if (workspaceEvent.Kind == WorkspaceChangeKind.ProjectAdded ||
                    workspaceEvent.Kind == WorkspaceChangeKind.ProjectChanged ||
                    workspaceEvent.Kind == WorkspaceChangeKind.ProjectReloaded ||
                    workspaceEvent.Kind == WorkspaceChangeKind.ProjectRemoved)
                {
                    _cache.TryRemove(workspaceEvent.ProjectId, out _);
                }
            };
        }

        public ImmutableArray<CodeFixProvider> GetAllCodeFixesForProject(ProjectId projectId)
        {
            if (_cache.ContainsKey(projectId))
                return _cache[projectId];

            var project = _workspace.CurrentSolution.GetProject(projectId);

            if (project == null)
            {
                _cache.TryRemove(projectId, out _);
                return ImmutableArray<CodeFixProvider>.Empty;
            }

            return LoadFrom(project);
        }

        private ImmutableArray<CodeFixProvider> LoadFrom(Project project)
        {
            var codeFixesFromProjectReferences = project.AnalyzerReferences
                .OfType<AnalyzerFileReference>()
                .SelectMany(analyzerFileReference => analyzerFileReference.GetAssembly().DefinedTypes)
                .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CodeFixProvider)))
                .Select(x =>
                {
                    try
                    {
                        var attribute = x.GetCustomAttribute<ExportCodeFixProviderAttribute>();
                        if (attribute == null)
                        {
                            _logger.LogTrace($"Skipping code fix provider '{x.AsType()}' because it is missing the ExportCodeFixProviderAttribute.");
                            return null;
                        }

                        if (attribute.Languages == null || !attribute.Languages.Contains(project.Language))
                        {
                            _logger.LogInformation($"Skipping code fix provider '{x.AsType()}' because its language '{attribute.Languages?.FirstOrDefault()}' doesn't match '{project.Language}'.");
                            return null;
                        }

                        return x.AsType().CreateInstance<CodeFixProvider>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Creating instance of code fix provider '{x.AsType()}' failed, error: {ex}");
                        return null;
                    }
                })
                .Where(x => x != null);

            var builtInCodeFixes = _providers.SelectMany(provider => provider.CodeFixProviders);

            var allCodeFixes = builtInCodeFixes.Concat(codeFixesFromProjectReferences).ToImmutableArray();

            _cache.AddOrUpdate(project.Id, allCodeFixes, (_, __) => allCodeFixes);

            return allCodeFixes;
        }
    }
}
