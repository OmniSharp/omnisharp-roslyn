using System.Collections.Generic;
using OmniSharp.DotNet.Cache;

namespace OmniSharp.DotNet.Models
{
    public class DotNetWorkspaceInformation
    {
        public DotNetWorkspaceInformation(IEnumerable<ProjectEntry> entries, bool includeSourceFiles = false)
        {
            var projects = new List<DotNetProjectInformation>();

            foreach (var entry in entries)
            {
                projects.Add(new DotNetProjectInformation(entry, includeSourceFiles));
            }

            this.Projects = projects;
        }

        public IEnumerable<DotNetProjectInformation> Projects { get; }
        public string RuntimePath { get; }
    }
}
