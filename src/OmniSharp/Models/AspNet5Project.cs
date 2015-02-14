using OmniSharp.AspNet5;
using System.Collections.Generic;
using System.Linq;

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

        public AspNet5Project(Project p)
        {
            Path = p.Path;
            Name = p.Name;
            Commands = p.Commands;
            Configurations = p.Configurations;
            GlobalJsonPath = p.GlobalJsonPath;
            ProjectSearchPaths = p.ProjectSearchPaths;
            Frameworks = p.ProjectsByFramework.Keys.ToList();
        }
    }
}