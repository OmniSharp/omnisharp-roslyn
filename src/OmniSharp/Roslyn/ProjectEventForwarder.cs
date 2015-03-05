using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.AspNet5;
using OmniSharp.Models;
using OmniSharp.MSBuild;
using OmniSharp.Services;

namespace OmniSharp.Roslyn
{
    public class ProjectEventForwarder
    {
        private readonly AspNet5Context _aspnet5Context;
        private readonly MSBuildContext _msbuildContext;
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEventEmitter _emitter;
        private readonly ISet<SimpleWorkspaceEvent> _queue = new HashSet<SimpleWorkspaceEvent>();
        private readonly object _lock = new object();

        public ProjectEventForwarder(AspNet5Context aspnet5Context, MSBuildContext msbuildContext, OmnisharpWorkspace workspace, IEventEmitter emitter)
        {
            _aspnet5Context = aspnet5Context;
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
                case WorkspaceChangeKind.ProjectChanged:
                case WorkspaceChangeKind.ProjectReloaded:
                    e = new SimpleWorkspaceEvent(args.NewSolution.GetProject(args.ProjectId).FilePath, args.Kind);
                    break;
                case WorkspaceChangeKind.ProjectRemoved:
                    e = new SimpleWorkspaceEvent(args.OldSolution.GetProject(args.ProjectId).FilePath, args.Kind);
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
                                if (e.Kind != WorkspaceChangeKind.ProjectRemoved)
                                {
                                    payload = GetProjectInformation(e.FileName);
                                }
                                _emitter.Emit(e.Kind.ToString(), payload);
                            }
                        });
                    }
                }
            }
        }

        private ProjectInformationResponse GetProjectInformation(string fileName)
        {
            var msBuildContextProject = _msbuildContext.GetProject(fileName);
            var aspNet5ContextProject = _aspnet5Context.GetProject(fileName);

            return new ProjectInformationResponse
            {
                MsBuildProject = msBuildContextProject != null ? new MSBuildProject(msBuildContextProject) : null,
                AspNet5Project = aspNet5ContextProject != null ? new AspNet5Project(aspNet5ContextProject) : null
            };
        }


        private class SimpleWorkspaceEvent
        {
            public string FileName { get; private set; }
            public WorkspaceChangeKind Kind { get; private set; }

            public SimpleWorkspaceEvent(string fileName, WorkspaceChangeKind kind)
            {
                FileName = fileName;
                Kind = kind;
            }

            public override bool Equals(object obj)
            {
                var other = obj as SimpleWorkspaceEvent;
                return other != null && Kind == other.Kind && FileName == other.FileName;
            }

            public override int GetHashCode()
            {
                return Kind.GetHashCode() * 23 + FileName.GetHashCode();
            }
        }
    }
}
