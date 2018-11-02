using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MSB = Microsoft.Build;

namespace OmniSharp.MSBuild
{
    class SolutionSdkFileUtil
    {
        static MSB.Evaluation.Project GetEvaluatedProject(string filePath,
            IDictionary<string, string> globalProperties = null)
        {
            var projectCollection = globalProperties != null
                ? new MSB.Evaluation.ProjectCollection(globalProperties)
                : new MSB.Evaluation.ProjectCollection();
            return projectCollection.LoadProject(filePath);
        }

        /// <summary>evaluating MSBuild.SolutionSDK(slnproj) project file and return the list of project paths.</summary>
        /// <remarks>returned paths are raw string of evaluated values(in most case, relative from slnproj), so you may have to resolve absolute path.</remarks>
        public static ImmutableArray<string> GetEvaluatedProjectFilePaths(string filePath,
            IDictionary<string, string> globalProperties = null,
            string[] allowedExtensions = null)
        {
            var project = GetEvaluatedProject(filePath, globalProperties);
            return project.AllEvaluatedItems.Where(x => x.ItemType == "Project"
                && (allowedExtensions == null || allowedExtensions.Any(ext => x.EvaluatedInclude.EndsWith(ext, StringComparison.OrdinalIgnoreCase))))
                .Select(x => x.EvaluatedInclude)
                .ToImmutableArray();
        }
    }
}
