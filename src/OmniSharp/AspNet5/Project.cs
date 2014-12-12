using System;
using System.Collections.Concurrent;

namespace OmniSharp.AspNet5
{
    public class Project
    {
        public int ContextId { get; set; }

        public string Path { get; set; }

        public ConcurrentDictionary<string, FrameworkProject> ProjectsByFramework { get; private set; }

        public Project()
        {
            ProjectsByFramework = new ConcurrentDictionary<string, FrameworkProject>();
        }
    }
}