using Microsoft.DotNet.ProjectModel;
using System.Collections.Generic;

namespace OmniSharp.DotNet.Models
{
    internal class DotNetWorkspaceInformation
    {
        public DotNetWorkspaceInformation(IEnumerable<ProjectContext> projectContexts, string configuration, bool includeSourceFiles = false)
        {
            var projects = new List<DotNetProjectInformation>();

            foreach (var projectContext in projectContexts)
            {
                projects.Add(new DotNetProjectInformation(projectContext, configuration, includeSourceFiles));
            }

            this.Projects = projects;
        }

        public IEnumerable<DotNetProjectInformation> Projects { get; }
    }
}
