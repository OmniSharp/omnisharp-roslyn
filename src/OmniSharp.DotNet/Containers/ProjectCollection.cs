using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.ProjectModel.ProjectSystem;
using NuGet.Frameworks;
using OmniSharp.DotNet.Models;

namespace OmniSharp.DotNet.Containers
{
    public class ProjectCollection
    {
        private readonly ConcurrentDictionary<string, List<ProjectWithFramework>> _projects;

        public ProjectCollection()
        {
            _projects = new ConcurrentDictionary<string, List<ProjectWithFramework>>();
        }

        public ProjectId Add(string projectPath, NuGetFramework targetFramework, ProjectInformation information)
        {
            // internally use Guid.NewGuid(), which is thread-safe
            var id = ProjectId.CreateNewId();

            _projects.AddOrUpdate(
                projectPath,
                addValueFactory: _ =>
                {
                    var frameworks = new List<ProjectWithFramework>()
                    {
                        new ProjectWithFramework(id, projectPath, targetFramework, information)
                    };
                    return frameworks;
                },
                updateValueFactory: (_, frameworks) =>
                {
                    frameworks.Add(new ProjectWithFramework(id, projectPath, targetFramework, information));

                    return frameworks;
                });

            return id;
        }

        public IEnumerable<ProjectWithFramework> Get(string projectPath)
        {
            List<ProjectWithFramework> result;
            if (_projects.TryGetValue(projectPath, out result))
            {
                return result;
            }
            else
            {
                return Enumerable.Empty<ProjectWithFramework>();
            }
        }

        public ISet<string> GetKeys()
        {
            return new HashSet<string>(_projects.Keys);
        }

        public IEnumerable<ProjectWithFramework> GetValues()
        {
            // flatten
            return _projects.Values.SelectMany(each => each);
        }

        public IEnumerable<ProjectWithFramework> Remove(string projectPath)
        {
            List<ProjectWithFramework> removed;
            if (_projects.TryRemove(projectPath, out removed))
            {
                return removed;
            }
            else
            {
                return Enumerable.Empty<ProjectWithFramework>();
            }
        }
    }
}
