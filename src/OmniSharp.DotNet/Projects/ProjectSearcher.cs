using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;

namespace OmniSharp.DotNet.Projects
{
    public class ProjectSearcher
    {
        public static IEnumerable<string> Search(string solutionRoot)
        {
            return Search(solutionRoot, maxDepth: 5);
        }

        public static IEnumerable<string> Search(string solutionRoot, int maxDepth)
        {
            var dir = new DirectoryInfo(solutionRoot);
            if (!dir.Exists)
            {
                return Enumerable.Empty<string>();
            }

            if (File.Exists(Path.Combine(solutionRoot, Project.FileName)))
            {
                return new string[] { solutionRoot };
            }
            else if (File.Exists(Path.Combine(solutionRoot, GlobalSettings.FileName)))
            {
                return FindProjectsThroughGlobalJson(solutionRoot);
            }
            else
            {
                return FindProjects(solutionRoot, maxDepth);
            }
        }

        private static IEnumerable<string> FindProjects(string root, int maxDepth)
        {
            var result = new List<string>();
            var stack = new Stack<Tuple<DirectoryInfo, int>>();

            stack.Push(Tuple.Create(new DirectoryInfo(root), 0));

            while (stack.Any())
            {
                var next = stack.Pop();
                var currentFolder = next.Item1;
                var depth = next.Item2;

                if (!currentFolder.Exists)
                {
                    continue;
                }
                else if (currentFolder.GetFiles(Project.FileName).Any())
                {
                    result.Add(Path.Combine(currentFolder.FullName, Project.FileName));
                }
                else if (depth < maxDepth)
                {
                    foreach (var sub in currentFolder.GetDirectories())
                    {
                        stack.Push(Tuple.Create(sub, depth + 1));
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
                return globalSettings.ProjectSearchPaths
                                     .Select(searchPath => Path.Combine(globalSettings.DirectoryPath, searchPath))
                                     .Where(actualPath => Directory.Exists(actualPath))
                                     .SelectMany(actualPath => Directory.GetDirectories(actualPath))
                                     .Where(actualPath => File.Exists(Path.Combine(actualPath, Project.FileName)))
                                     .Select(path => Path.GetFullPath(path))
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .ToList();
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }
    }
}