using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using OmniSharp.DotNet.Projects;

namespace OmniSharp.DotNet
{
    public class DotNetWorkspace : Workspace
    {
        private readonly HashSet<string> _projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool _needRefresh;

        public DotNetWorkspace(string initialPath) : base(ProjectReaderSettings.ReadFromEnvironment(), true)
        {
            foreach (var path in ProjectSearcher.Search(initialPath))
            {
                AddProject(path);
            }
        }

        public void AddProject(string path)
        {
            var projectPath = ProjectPathHelper.NormalizeProjectDirectoryPath(path);

            if (projectPath != null)
            {
                _needRefresh = _projects.Add(path);
            }
        }

        public void RemoveProject(string path)
        {
            _needRefresh = _projects.Remove(path);
        }

        public IReadOnlyList<string> GetAllProjects()
        {
            Refresh();
            return _projects.ToList().AsReadOnly();
        }

        public IReadOnlyList<ProjectContext> GetProjectContexts(string projectPath)
        {
            return (IReadOnlyList<ProjectContext>)GetProjectContextCollection(projectPath)?.ProjectContexts.AsReadOnly() ??
                   Array.Empty<ProjectContext>();
        }

        /// <summary>
        /// Refresh all cached projects in the Workspace
        /// </summary>
        public void Refresh()
        {
            if (!_needRefresh)
            {
                return;
            }

            var basePaths = new List<string>(_projects);
            _projects.Clear();

            foreach (var projectDirectory in basePaths)
            {
                var project = GetProject(projectDirectory);
                if (project == null)
                {
                    continue;
                }

                _projects.Add(project.ProjectDirectory);

                foreach (var projectContext in GetProjectContextCollection(project.ProjectDirectory).ProjectContexts)
                {
                    foreach (var reference in GetProjectReferences(projectContext))
                    {
                        var referencedProject = GetProject(reference.Path);
                        if (referencedProject != null)
                        {
                            _projects.Add(referencedProject.ProjectDirectory);
                        }
                    }
                }
            }

            _needRefresh = false;
        }

        protected override IEnumerable<ProjectContext> BuildProjectContexts(Project project)
        {
            foreach (var framework in project.GetTargetFrameworks())
            {
                yield return CreateBaseProjectBuilder(project)
                    .AsDesignTime()
                    .WithTargetFramework(framework.FrameworkName)
                    .Build();
            }
        }

        private static IEnumerable<ProjectDescription> GetProjectReferences(ProjectContext context)
        {
            var projectDescriptions = context.LibraryManager
                                             .GetLibraries()
                                             .Where(lib => lib.Identity.Type == LibraryType.Project)
                                             .OfType<ProjectDescription>();

            foreach (var description in projectDescriptions)
            {
                if (description.Identity.Name == context.ProjectFile.Name)
                {
                    continue;
                }

                // if this is an assembly reference then don't treat it as project reference
                if (!string.IsNullOrEmpty(description.TargetFrameworkInfo?.AssemblyPath))
                {
                    continue;
                }

                yield return description;
            }
        }
    }
}