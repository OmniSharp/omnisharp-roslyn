using System;
using System.Linq;
using System.Collections.Generic;
using OmniSharp.AspNet5;

namespace OmniSharp.Models
{
    public class WorkspaceInformationResponse
    {
        public AspNet5WorkspaceInformation AspNet5 { get; set; }
        public MsBuildWorkspaceInformation MSBuild { get; set; }
    }

    public class MsBuildWorkspaceInformation
    {

    }

    public class AspNet5WorkspaceInformation
    {
        private AspNet5Context _context;

        public AspNet5WorkspaceInformation(AspNet5Context context)
        {
            _context = context;

            Projects = context.Projects.Values.Select(project => new AspNet5Project
            {
                Path = project.Path,
                Name = project.Name,
                Commands = project.Commands,
                Configurations = project.Configurations,
                GlobalJsonPath = project.GlobalJsonPath,
                ProjectSearchPaths = project.ProjectSearchPaths
            });

            RuntimePath = context.RuntimePath;
        }

        public IEnumerable<AspNet5Project> Projects { get; }
        public string RuntimePath { get; }
    }

    public class AspNet5Project
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public IDictionary<string, string> Commands { get; set; }
        public IList<string> Configurations { get; set; }
        public IList<string> ProjectSearchPaths { get; set; }
        public string GlobalJsonPath { get; set; }
    }
}