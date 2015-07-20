using System.Collections.Generic;
using System.Linq;
using OmniSharp.Dnx;

namespace OmniSharp.Models
{
    public class DnxProject
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public IDictionary<string, string> Commands { get; set; }
        public IList<string> Configurations { get; set; }
        public IList<string> ProjectSearchPaths { get; set; }
        public IList<DnxFramework> Frameworks { get; set; }
        public string GlobalJsonPath { get; set; }
        public IList<string> SourceFiles { get; set; }

        public DnxProject(Project project)
        {
            Path = project.Path;
            Name = project.Name;
            Commands = project.Commands;
            Configurations = project.Configurations;
            GlobalJsonPath = project.GlobalJsonPath;
            ProjectSearchPaths = project.ProjectSearchPaths;
            Frameworks = project.ProjectsByFramework.Values.Select(framework => new DnxFramework(framework)).ToList();
            SourceFiles = project.SourceFiles;
        }
    }
}
