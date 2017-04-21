using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using OmniSharp.DotNet.Models;
using OmniSharp.Eventing;
using OmniSharp.Models.Events;
using OmniSharp.Models.ProjectInformation;

namespace OmniSharp.DotNet.Cache
{
    public class ProjectStatesCache
    {
        private readonly Dictionary<string, ProjectEntry> _projects
                   = new Dictionary<string, ProjectEntry>(StringComparer.OrdinalIgnoreCase);

        private readonly ILogger _logger;
        private readonly IEventEmitter _emitter;

        public ProjectStatesCache(ILoggerFactory loggerFactory, IEventEmitter emitter)
        {
            _logger = loggerFactory.CreateLogger<ProjectStatesCache>();
            _emitter = emitter;
        }

        public IEnumerable<ProjectEntry> GetStates => _projects.Values;

        public IReadOnlyCollection<ProjectState> GetValues()
        {
            return _projects.Select(p => p.Value)
                            .SelectMany(entry => entry.ProjectStates)
                            .ToList();
        }

        public void Update(string projectDirectory,
                           IEnumerable<ProjectContext> contexts,
                           Action<ProjectId, ProjectContext> addAction,
                           Action<ProjectId> removeAction)
        {
            _logger.LogDebug($"Updating project ${projectDirectory}");

            var entry = GetOrAddEntry(projectDirectory, out bool added);

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

            var projectInformation = new DotNetProjectInformation(entry);
            if (added)
            {
                EmitProject(EventTypes.ProjectChanged, projectInformation);
            }
            else
            {
                EmitProject(EventTypes.ProjectAdded, projectInformation);
            }
        }

        /// <summary>
        /// Remove projects not in the give project set and execute the <paramref name="removeAction"/> on the removed project id.
        /// </summary>
        /// <param name="perservedProjects">Projects to perserve</param>
        /// <param name="removeAction"></param>
        public void RemoveExcept(IEnumerable<string> perservedProjects, Action<ProjectEntry> removeAction)
        {
            var removeList = new HashSet<string>(_projects.Keys, StringComparer.OrdinalIgnoreCase);
            removeList.ExceptWith(perservedProjects);

            foreach (var key in removeList)
            {
                var entry = _projects[key];
                var projectInformation = new DotNetProjectInformation(entry);

                EmitProject(EventTypes.ProjectRemoved, projectInformation);
                removeAction(entry);

                _projects.Remove(key);
            }
        }

        public IEnumerable<ProjectState> Find(string projectDirectory)
        {
            if (_projects.TryGetValue(projectDirectory, out ProjectEntry entry))
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
            if (_projects.TryGetValue(projectDirectory, out ProjectEntry entry))
            {
                return entry.Get(framework);
            }
            else
            {
                return null;
            }
        }

        internal ProjectEntry GetEntry(string projectDirectory)
        {
            if (_projects.TryGetValue(projectDirectory, out ProjectEntry result))
            {
                return result;
            }

            return null;
        }

        private ProjectEntry GetOrAddEntry(string filePath, out bool added)
        {
            added = false;
            var result = GetEntry(filePath);

            if (result == null)
            {
                result = new ProjectEntry(filePath);
                _projects[filePath] = result;
                added = true;
            }

            return result;
        }

        private void EmitProject(string eventType, DotNetProjectInformation information)
        {
            _emitter.Emit(
                eventType,
                new ProjectInformationResponse()
                {
                    { "DotNetProject", information }
                });
        }
    }
}
