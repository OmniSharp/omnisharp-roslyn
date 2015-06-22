using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Dnx;
using OmniSharp.Models;
using OmniSharp.MSBuild;
using OmniSharp.Services;

namespace OmniSharp.Roslyn
{
    public class ProjectEventForwarder
    {
        private readonly DnxContext _dnxContext;
        private readonly MSBuildContext _msbuildContext;
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEventEmitter _emitter;
        private readonly ISet<SimpleWorkspaceEvent> _queue = new HashSet<SimpleWorkspaceEvent>();
        private readonly object _lock = new object();

        public ProjectEventForwarder(DnxContext dnxContext, MSBuildContext msbuildContext, OmnisharpWorkspace workspace, IEventEmitter emitter)
        {
            _dnxContext = dnxContext;
            _msbuildContext = msbuildContext;
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

                            lock (_lock)
                            {
                                _queue.Remove(e);

                                object payload = null;
                                if (e.EventType != EventTypes.ProjectRemoved)
                                {
                                    payload = GetProjectInformation(e.FileName);
                                }
                                _emitter.Emit(e.EventType, payload);
                            }
                        });
                    }
                }
            }
        }

        private ProjectInformationResponse GetProjectInformation(string fileName)
        {
            var msBuildContextProject = _msbuildContext.GetProject(fileName);
            var dnxContextProject = _dnxContext.GetProject(fileName);

            return new ProjectInformationResponse
            {
                MsBuildProject = msBuildContextProject != null ? new MSBuildProject(msBuildContextProject) : null,
                DnxProject = dnxContextProject != null ? new DnxProject(dnxContextProject) : null
            };
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
