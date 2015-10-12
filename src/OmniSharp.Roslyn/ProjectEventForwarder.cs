using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp.Roslyn
{
    [Export]
    public class ProjectEventForwarder
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEventEmitter _emitter;
        private readonly ISet<SimpleWorkspaceEvent> _queue = new HashSet<SimpleWorkspaceEvent>();
        private readonly object _lock = new object();
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        [ImportingConstructor]
        public ProjectEventForwarder(OmnisharpWorkspace workspace, [ImportMany] IEnumerable<IProjectSystem> projectSystems, IEventEmitter emitter)
        {
            _projectSystems = projectSystems;
            _workspace = workspace;
            _emitter = emitter;
            _workspace.WorkspaceChanged += OnWorkspaceChanged;
        }

        private void OnWorkspaceChanged(object source, WorkspaceChangeEventArgs args)
        {
            SimpleWorkspaceEvent e = null;

            switch (args.Kind)
            {
                case WorkspaceChangeKind.ProjectAdded:
                    e = new SimpleWorkspaceEvent(args.NewSolution.GetProject(args.ProjectId).FilePath, EventTypes.ProjectAdded);
                    break;
                case WorkspaceChangeKind.ProjectChanged:
                case WorkspaceChangeKind.ProjectReloaded:
                    e = new SimpleWorkspaceEvent(args.NewSolution.GetProject(args.ProjectId).FilePath, EventTypes.ProjectChanged);
                    break;
                case WorkspaceChangeKind.ProjectRemoved:
                    e = new SimpleWorkspaceEvent(args.OldSolution.GetProject(args.ProjectId).FilePath, EventTypes.ProjectRemoved);
                    break;
            }

            if (e != null)
            {
                lock (_lock)
                {
                    var removed = _queue.Remove(e);
                    _queue.Add(e);
                    if (!removed)
                    {
                        Task.Factory.StartNew(async () =>
                        {
                            await Task.Delay(500);

                            object payload = null;
                            if (e.EventType != EventTypes.ProjectRemoved)
                            {
                                payload = await GetProjectInformation(e.FileName);
                            }

                            lock (_lock)
                            {
                                _queue.Remove(e);
                                _emitter.Emit(e.EventType, payload);
                            }
                        });
                    }
                }
            }
        }

        private async Task<ProjectInformationResponse> GetProjectInformation(string fileName)
        {
            var response = new ProjectInformationResponse();

            foreach (var projectSystem in _projectSystems) {
                var project = await projectSystem.GetProjectModel(fileName);
                if (project != null)
                    response.Add($"{projectSystem.Key}Project", project);
            }

            return response;
        }

        private class SimpleWorkspaceEvent
        {
            public string FileName { get; private set; }
            public string EventType { get; private set; }

            public SimpleWorkspaceEvent(string fileName, string eventType)
            {
                FileName = fileName;
                EventType = eventType;
            }

            public override bool Equals(object obj)
            {
                var other = obj as SimpleWorkspaceEvent;
                return other != null && EventType == other.EventType && FileName == other.FileName;
            }

            public override int GetHashCode()
            {
                return EventType?.GetHashCode() * 23 + FileName?.GetHashCode() ?? 0;
            }
        }
    }
}
