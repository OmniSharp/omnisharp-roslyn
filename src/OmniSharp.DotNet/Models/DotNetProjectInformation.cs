using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.ProjectModel.ProjectSystem;

namespace OmniSharp.DotNet.Models
{
    internal class DotNetProjectInformation
    {
        public DotNetProjectInformation(string projectPath, ProjectInformation info)
        {
            Path = projectPath;
            Name = info.Name;
            Commands = info.Commands;
            Configurations = info.Configurations;
            ProjectSearchPaths = info.ProjectSearchPaths;
            Frameworks = info.Frameworks.Select(framework => new DotNetFramework(framework));
            GlobalJsonPath = info.GlobalJsonPath;
        }

        public string Path { get; }
        public string Name { get; }
        public IDictionary<string, string> Commands { get; }
        public IEnumerable<string> Configurations { get; }
        public IEnumerable<string> ProjectSearchPaths { get; }
        public IEnumerable<DotNetFramework> Frameworks { get; }
        public string GlobalJsonPath { get; }

        //public IList<string> SourceFiles { get; set; }
    }
}
