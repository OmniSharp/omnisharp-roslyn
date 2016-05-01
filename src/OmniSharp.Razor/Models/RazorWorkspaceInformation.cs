using System.Collections.Generic;
using OmniSharp.DotNet.Cache;

namespace OmniSharp.DotNet.Models
{
    public class RazorWorkspaceInformation
    {
        public RazorWorkspaceInformation(/* IEnumerable<ProjectEntry> entries, */bool includeSourceFiles = false)
        {
            var projects = new List<RazorProjectInformation>();

            /*foreach (var entry in entries)
            {
                projects.Add(new RazorProjectInformation(entry, includeSourceFiles));
            }*/

            this.Projects = projects;
        }

        public IEnumerable<RazorProjectInformation> Projects { get; }
        public string RuntimePath { get; }
    }
}
