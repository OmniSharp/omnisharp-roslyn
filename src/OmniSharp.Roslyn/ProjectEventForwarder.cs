using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Eventing;
using OmniSharp.Models.Events;
using OmniSharp.Models.ProjectInformation;
using OmniSharp.Services;

namespace OmniSharp.Roslyn
{
    [Export, Shared]
    public class ProjectEventForwarder
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEventEmitter _emitter;
        private readonly ISet<SimpleWorkspaceEvent> _queue = new HashSet<SimpleWorkspaceEvent>();
        private readonly object _lock = new object();
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
        }

        public void Initialize()
        {
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
                                payload = await GetProjectInformationAsync(e.FileName);
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

            public override bool Equals(object obj)
            {
                var other = obj as SimpleWorkspaceEvent;
                return other != null
                    && EventType == other.EventType
                    && FileName == other.FileName;
            }

            public override int GetHashCode() =>
                EventType?.GetHashCode() * 23 + FileName?.GetHashCode() ?? 0;
        }
    }
}
