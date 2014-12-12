using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace OmniSharp.AspNet5
{
    public class AspNet5Context
    {
        private int _contextId;

        public string RuntimePath { get; set; }

        public string HostId { get; private set; }

        public int DesignTimeHostPort { get; set; }

        public Dictionary<string, int> ProjectContextMapping { get; set; }

        public Dictionary<int, ProjectState> Projects { get; set; }

        public Dictionary<ProjectId, FrameworkState> WorkspaceMapping { get; set; }

        public ProcessingQueue Connection { get; set; }

        public AspNet5Context()
        {
            HostId = Guid.NewGuid().ToString();
            DesignTimeHostPort = 1334;
            ProjectContextMapping = new Dictionary<string, int>();
            Projects = new Dictionary<int, ProjectState>();
            WorkspaceMapping = new Dictionary<ProjectId, FrameworkState>();
        }

        public bool TryAddProject(string projectFile, out int contextId)
        {
            contextId = -1;
            if (ProjectContextMapping.ContainsKey(projectFile))
            {
                return false;
            }

            contextId = ++_contextId;

            // Create a mapping from path to contextid and back
            ProjectContextMapping[projectFile] = contextId;
            Projects[contextId] = new ProjectState
            {
                Path = projectFile,
                ContextId = contextId
            };

            return true;
        }
    }
}