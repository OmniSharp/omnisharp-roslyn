using OmniSharp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.Helpers
{
    public static class ProjectSystemExtensions
    {
        public static async Task WaitForAllProjectsToLoadForFileAsync(this IEnumerable<IProjectSystem> projectSystems, string filePath)
        {
            if (filePath != null)
            {
                await Task.WhenAll(GetProjectSystemsForFile(projectSystems, filePath).Select(ps => ps.WaitForProjectsToLoadForFileAsync(filePath)));
            }
        }

        private static IEnumerable<IProjectSystem> GetProjectSystemsForFile(IEnumerable<IProjectSystem> projectSystems, string filePath)
        {
            foreach (IProjectSystem projectSystem in projectSystems)
            {
                if (projectSystem.Extensions.Any(extension => filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return projectSystem;
                }
            }
        }
    }
}
