using System.Collections.Generic;
using OmniSharp.DotNet.Cache;

namespace OmniSharp.DotNet.Models
{
    public class DotNetWorkspaceInfo
    {
        public DotNetWorkspaceInfo(IEnumerable<ProjectEntry> entries, bool includeSourceFiles = false)
        {
            var projects = new List<DotNetProjectInfo>();

            foreach (var entry in entries)
            {
                projects.Add(new DotNetProjectInfo(entry, includeSourceFiles));
            }

            this.Projects = projects;
        }

        public IList<DotNetProjectInfo> Projects { get; }
        public string RuntimePath { get; }
    }
}
