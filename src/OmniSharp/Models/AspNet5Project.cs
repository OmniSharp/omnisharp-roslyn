using System.Collections.Generic;
using System.Linq;
using OmniSharp.AspNet5;

namespace OmniSharp.Models
{
    public class AspNet5Project
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public IDictionary<string, string> Commands { get; set; }
        public IList<string> Configurations { get; set; }
        public IList<string> ProjectSearchPaths { get; set; }
        public IList<string> Frameworks { get; set; }
        public string GlobalJsonPath { get; set; }
        public IList<string> SourceFiles { get; set; }

        public AspNet5Project(Project project)
        {
            Path = project.Path;
            Name = project.Name;
            Commands = project.Commands;
            Configurations = project.Configurations;
            GlobalJsonPath = project.GlobalJsonPath;
            ProjectSearchPaths = project.ProjectSearchPaths;
            Frameworks = project.ProjectsByFramework.Keys.ToList();
            SourceFiles = project.SourceFiles;
        }
    }
}