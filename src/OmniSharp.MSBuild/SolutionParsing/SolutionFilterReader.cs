using System;
using System.Collections.Immutable;
using System.IO;
using Newtonsoft.Json.Linq;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal static class SolutionFilterReader
    {
        public static bool IsSolutionFilterFilename(string filename)
        {
            return Path.GetExtension(filename).Equals(".slnf", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryRead(string filterFilename, out string solutionFilename, out ImmutableHashSet<string> projectFilter)
        {
            try
            {
                var filterDirectory = Path.GetDirectoryName(filterFilename);

                var document = JObject.Parse(File.ReadAllText(filterFilename));
                var solution = document["solution"];
                // Convert directory separators to the platform's default, since that is what MSBuild provide us.
                var solutionPath = ((string)solution?["path"])?.Replace('\\', Path.DirectorySeparatorChar);

                solutionFilename = Path.GetFullPath(Path.Combine(filterDirectory, solutionPath));
                if (!File.Exists(solutionFilename))
                {
                    projectFilter = ImmutableHashSet<string>.Empty;
                    return false;
                }

                // The base directory for projects is the solution folder.
                var solutionDirectory = Path.GetDirectoryName(solutionFilename);

                var filterProjects = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
                var projects = (JArray)solution?["projects"] ?? new JArray();
                foreach (string project in projects)
                {
                    // Convert directory separators to the platform's default, since that is what MSBuild provide us.
                    var projectPath = project?.Replace('\\', Path.DirectorySeparatorChar);
                    if (projectPath is null)
                    {
                        projectFilter = ImmutableHashSet<string>.Empty;
                        return false;
                    }

                    var projectFilename = Path.GetFullPath(Path.Combine(solutionDirectory, projectPath));
                    if (!File.Exists(projectFilename))
                    {
                        projectFilter = ImmutableHashSet<string>.Empty;
                        return false;
                    }

                    // Fill the filter with the absolute project paths.
                    filterProjects.Add(projectFilename);
                }

                projectFilter = filterProjects.ToImmutable();
                return true;
            }
            catch
            {
                solutionFilename = string.Empty;
                projectFilter = ImmutableHashSet<string>.Empty;
                return false;
            }
        }
    }
}
