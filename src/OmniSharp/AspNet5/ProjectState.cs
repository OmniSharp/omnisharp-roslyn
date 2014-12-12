using System;
using System.Collections.Concurrent;

namespace OmniSharp.AspNet5
{
    public class ProjectState
    {
        public int ContextId { get; set; }

        public string Path { get; set; }

        public ConcurrentDictionary<string, FrameworkState> ProjectsByFramework { get; private set; }

        public ProjectState()
        {
            ProjectsByFramework = new ConcurrentDictionary<string, FrameworkState>();
        }
    }
}