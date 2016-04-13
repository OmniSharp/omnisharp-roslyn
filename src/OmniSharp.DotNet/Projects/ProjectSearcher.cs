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
                return FindProjects(solutionRoot);
            }
        }

        private static IEnumerable<string> FindProjects(string root)
        {
            var result = new List<string>();
            var stack = new Stack<DirectoryInfo>();
            stack.Push(new DirectoryInfo(root));

            while (stack.Any())
            {
                var next = stack.Pop();
                if (!next.Exists)
                {
                    continue;
                }
                else if (next.GetFiles(Project.FileName).Any())
                {
                    result.Add(Path.Combine(next.FullName, Project.FileName));
                }
                else
                {
                    foreach (var sub in next.GetDirectories())
                    {
                        stack.Push(sub);
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