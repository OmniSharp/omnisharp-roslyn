using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OmniSharp.Dnx
{
    public class Project
    {
        public int ContextId { get; set; }

        public string Path { get; set; }

        public string Name { get; set; }

        public IList<string> Configurations { get; set; }

        public IDictionary<string, string> Commands { get; set; }

        public IList<string> ProjectSearchPaths { get; set; }

        public string GlobalJsonPath { get; set; }

        public ConcurrentDictionary<string, FrameworkProject> ProjectsByFramework { get; private set; }

        public bool InitializeSent { get; set; }

        public IList<string> SourceFiles { get; set; }

        public Project()
        {
            ProjectsByFramework = new ConcurrentDictionary<string, FrameworkProject>();
            Commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return Name + " (" + Path + ")";
        }
    }
}