using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Eventing;
using OmniSharp.Models.Events;
using OmniSharp.Models.ProjectInformation;
using OmniSharp.Services;
using System.Threading;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn
{
    [Export, Shared]
    public class ProjectEventForwarder
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEventEmitter _emitter;
        private readonly ConcurrentDictionary<SimpleWorkspaceEvent, AsyncLock> _eventLocks;
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        [ImportingConstructor]
        public ProjectEventForwarder(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<IProjectSystem> projectSystems,
            IEventEmitter emitter)
        {
            _projectSystems = projectSystems;
            _workspace = workspace;
            _emitter = emitter;
            _eventLocks = new ConcurrentDictionary<SimpleWorkspaceEvent, AsyncLock>();
        }

        public void Initialize()
        {
            _workspace.WorkspaceChanged += OnWorkspaceChanged;
        }

        private async void OnWorkspaceChanged(object source, WorkspaceChangeEventArgs args)
        {
            SimpleWorkspaceEvent workspaceEvent = null;

            switch (args.Kind)
            {
                case WorkspaceChangeKind.ProjectAdded:
                    workspaceEvent = new SimpleWorkspaceEvent(args.NewSolution.GetProject(args.ProjectId).FilePath, EventTypes.ProjectAdded);
                    break;
                case WorkspaceChangeKind.ProjectChanged:
                case WorkspaceChangeKind.ProjectReloaded:
                    workspaceEvent = new SimpleWorkspaceEvent(args.NewSolution.GetProject(args.ProjectId).FilePath, EventTypes.ProjectChanged);
                    break;
                case WorkspaceChangeKind.ProjectRemoved:
                    workspaceEvent = new SimpleWorkspaceEvent(args.OldSolution.GetProject(args.ProjectId).FilePath, EventTypes.ProjectRemoved);
                    break;
                default:
                    return;
            }

            var added = false;
            using (await _eventLocks.GetOrAdd(workspaceEvent, _ => { added = true; return new AsyncLock(); }).LockAsync())
            {
                // We are already processing a similar event, no need send it again
                if (added)
                {
                    object payload = null;

                    try
                    {
                        if (workspaceEvent.EventType != EventTypes.ProjectRemoved)
                        {
                            // Project information should be up-to-date so there's no need to wait.
                            payload = await GetProjectInformationAsync(workspaceEvent.FileName);
                        }

                        _emitter.Emit(workspaceEvent.EventType, payload);
                    }
                    finally
                    {
                        _eventLocks.TryRemove(workspaceEvent, out _);
                    }
                }
            }
        }

        private async Task<ProjectInformationResponse> GetProjectInformationAsync(string fileName)
        {
            var response = new ProjectInformationResponse();

            foreach (var projectSystem in _projectSystems.Where(project => project.Initialized))
            {
                var project = await projectSystem.GetProjectModelAsync(fileName);
                if (project != null)
                {
                    response.Add($"{projectSystem.Key}Project", project);
                }
            }

            return response;
        }

        private class SimpleWorkspaceEvent
        {
            public string FileName { get; }
            public string EventType { get; }

            public SimpleWorkspaceEvent(string fileName, string eventType)
            {
                FileName = fileName;
                EventType = eventType;
            }

            public override bool Equals(object obj) =>
                obj is SimpleWorkspaceEvent other
                    && EventType == other.EventType
                    && FileName == other.FileName;

            public override int GetHashCode() =>
                EventType?.GetHashCode() * 23 + FileName?.GetHashCode() ?? 0;
        }
    }
}
