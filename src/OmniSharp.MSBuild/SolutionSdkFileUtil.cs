using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MSB = Microsoft.Build;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild
{
    class SolutionSdkFileUtil
    {
        /// <summary>evaluating MSBuild.SolutionSDK(slnproj) project file and return the list of project paths.</summary>
        /// <remarks>returned paths are raw string of evaluated values(in most case, relative from slnproj), so you may have to resolve absolute path.</remarks>
        public static ImmutableArray<string> GetEvaluatedProjectFilePaths(string filePath,
            ProjectLoader loader,
            IDictionary<string, string> globalProperties = null,
            string[] allowedExtensions = null)
        {
            var project = loader.EvaluateProjectFile(filePath);
            return project.AllEvaluatedItems.Where(x => x.ItemType == "Project"
                && (allowedExtensions == null || allowedExtensions.Any(ext => x.EvaluatedInclude.EndsWith(ext, StringComparison.OrdinalIgnoreCase))))
                .Select(x => x.EvaluatedInclude)
                .ToImmutableArray();
        }
    }
}
