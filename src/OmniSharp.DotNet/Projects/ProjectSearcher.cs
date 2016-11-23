using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ProjectModel;

namespace OmniSharp.DotNet.Projects
{
    public class ProjectSearcher
    {
        public static IEnumerable<string> Search(string directory)
        {
            return Search(directory, maxDepth: 5);
        }

        public static IEnumerable<string> Search(string directory, int maxDepth)
        {
            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            // Is there a project.json file in this directory? If so, return it.
            var projectFilePath = Path.Combine(directory, Project.FileName);
            if (File.Exists(projectFilePath))
            {
                return new string[] { projectFilePath };
            }

            // Is there a global.json file in this directory? If so, use that to search.
            if (File.Exists(Path.Combine(directory, GlobalSettings.FileName)))
            {
                return FindProjectsThroughGlobalJson(directory);
            }

            // Otherwise, perform a general search through the file system.
            return FindProjects(directory, maxDepth);
        }

        // TODO: Replace with proper tuple when we move to C# 7
        private struct DirectoryAndDepth
        {
            public readonly string Directory;
            public readonly int Depth;

            private DirectoryAndDepth(string directory, int depth)
            {
                this.Directory = directory;
                this.Depth = depth;
            }

            public static DirectoryAndDepth Create(string directory, int depth)
                => new DirectoryAndDepth(directory, depth);
        }

        private static IEnumerable<string> FindProjects(string rootDirectory, int maxDepth)
        {
            var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<DirectoryAndDepth>();

            stack.Push(DirectoryAndDepth.Create(rootDirectory, 0));

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (!Directory.Exists(current.Directory))
                {
                    continue;
                }

                // Did we find a project.json?
                var projectFilePath = Path.Combine(current.Directory, Project.FileName);
                if (File.Exists(projectFilePath))
                {
                    result.Add(projectFilePath);
                }

                // If we're not already at maximum depth, go ahead and search child directories.
                if (current.Depth < maxDepth)
                {
                    foreach (var childDirectory in Directory.GetDirectories(current.Directory))
                    {
                        stack.Push(DirectoryAndDepth.Create(childDirectory, current.Depth + 1));
                    }
                }
            }

            return result;
        }

        private static IEnumerable<string> FindProjectsThroughGlobalJson(string root)
        {
            GlobalSettings globalSettings;
            if (GlobalSettings.TryGetGlobalSettings(root, out globalSettings))
            {
                var projectPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                var searchDirectories = new Queue<string>();

                // Look in global.json 'projects' search paths and their immediate children
                foreach (var searchPath in globalSettings.ProjectSearchPaths)
                {
                    var searchDirectory = Path.Combine(globalSettings.DirectoryPath, searchPath);
                    if (Directory.Exists(searchDirectory))
                    {
                        searchDirectories.Enqueue(searchDirectory);

                        foreach (var childDirectory in Directory.GetDirectories(searchDirectory))
                        {
                            searchDirectories.Enqueue(childDirectory);
                        }
                    }
                }

                while (searchDirectories.Count > 0)
                {
                    var searchDirectory = searchDirectories.Dequeue();
                    var projectFilePath = Path.Combine(searchDirectory, Project.FileName);
                    if (File.Exists(projectFilePath))
                    {
                        projectPaths.Add(Path.GetFullPath(projectFilePath));
                    }
                }

                return projectPaths;
            }

            return Array.Empty<string>();
        }
    }
}