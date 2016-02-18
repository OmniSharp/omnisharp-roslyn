using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;

namespace OmniSharp.DotNet.Cache
{
    public class ProjectStatesCache
    {
        private readonly Dictionary<string, ProjectEntry> _projects
                   = new Dictionary<string, ProjectEntry>(StringComparer.OrdinalIgnoreCase);

        private readonly ILogger _logger;

        public ProjectStatesCache(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<ProjectStatesCache>() ?? new DummyLogger<ProjectStatesCache>();
        }

        public IReadOnlyCollection<ProjectState> Values
        {
            get { return _projects.Select(p=>p.Value).SelectMany(entry=>entry.ProjectStates).ToList(); }
        }

        public void Update(string projectDirectory,
                           IEnumerable<ProjectContext> contexts,
                           Action<ProjectId, ProjectContext> addAction,
                           Action<ProjectId> removeAction)
        {
            _logger.LogDebug($"Updating project ${projectDirectory}");

            var entry = GetOrAddEntry(projectDirectory);

            // remove frameworks which don't exist after update
            var remove = entry.Frameworks.Except(contexts.Select(c => c.TargetFramework));
            foreach (var each in remove)
            {
                var toRemove = entry.Get(each);
                removeAction(toRemove.Id);
                entry.Remove(each);
            }

            foreach (var context in contexts)
            {
                _logger.LogDebug($"  For context {context.TargetFramework}");
                ProjectState currentState = entry.Get(context.TargetFramework);
                if (currentState != null)
                {
                    _logger.LogDebug($"  Update exsiting {nameof(ProjectState)}.");
                    currentState.ProjectContext = context;
                }
                else
                {
                    _logger.LogDebug($"  Add new {nameof(ProjectState)}.");
                    var projectId = ProjectId.CreateNewId();
                    entry.Set(new ProjectState(projectId, context));
                    addAction(projectId, context);
                }
            }
        }

        /// <summary>
        /// Remove projects not in the give project set and execute the <paramref name="removeAction"/> on the removed project id.
        /// </summary>
        /// <param name="perservedProjects">Projects to perserve</param>
        /// <param name="removeAction"></param>
        public void RemoveExcept(IEnumerable<string> perservedProjects, Action<ProjectId> removeAction)
        {
            var removeList = new HashSet<string>(_projects.Keys, StringComparer.OrdinalIgnoreCase);
            removeList.ExceptWith(perservedProjects);

            foreach (var key in removeList)
            {
                var entry = _projects[key];
                foreach (var state in entry.ProjectStates)
                {
                    removeAction(state.Id);
                }

                _projects.Remove(key);
            }
        }

        public IEnumerable<ProjectState> Find(string projectDirectory)
        {
            ProjectEntry entry;
            if (_projects.TryGetValue(projectDirectory, out entry))
            {
                return entry.ProjectStates;
            }
            else
            {
                return Enumerable.Empty<ProjectState>();
            }
        }

        public ProjectState Find(string projectDirectory, NuGetFramework framework)
        {
            ProjectEntry entry;
            if (_projects.TryGetValue(projectDirectory, out entry))
            {
                return entry.Get(framework);
            }
            else
            {
                return null;
            }
        }

        private ProjectEntry GetOrAddEntry(string projectDirectory)
        {
            ProjectEntry result;
            if (_projects.TryGetValue(projectDirectory, out result))
            {
                return result;
            }
            else
            {
                result = new ProjectEntry(projectDirectory);
                _projects[projectDirectory] = result;

                return result;
            }
        }

        private class ProjectEntry
        {
            private readonly Dictionary<NuGetFramework, ProjectState> _states
                       = new Dictionary<NuGetFramework, ProjectState>();

            public ProjectEntry(string projectDirectory)
            {
                ProjectDirectory = projectDirectory;
            }

            public string ProjectDirectory { get; }

            public IEnumerable<NuGetFramework> Frameworks => _states.Keys;

            public IEnumerable<ProjectState> ProjectStates => _states.Values;

            public ProjectState Get(NuGetFramework framework)
            {
                ProjectState result;
                if (_states.TryGetValue(framework, out result))
                {
                    return result;
                }
                else
                {
                    return null;
                }
            }

            public void Set(ProjectState state)
            {
                _states[state.ProjectContext.TargetFramework] = state;
            }

            public bool Remove(NuGetFramework framework)
            {
                return _states.Remove(framework);
            }

            public override string ToString()
            {
                return $"ProjectEntry {ProjectDirectory}, {_states.Count} states";
            }
        }
    }
}
