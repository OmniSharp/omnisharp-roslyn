using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using Newtonsoft.Json.Linq;
using Roslyn.Utilities;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal static class SolutionFileReader
    {
        public static bool IsSolutionFileFilename(string filename)
        {
            var extension = Path.GetExtension(filename);
            return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryRead(string solutionFilename, out ImmutableArray<SolutionFileProjectInfo> projects)
        {
            return TryRead(solutionFilename, ImmutableHashSet<string>.Empty, out projects);
        }
        // projects is a dictionary of format: ProjectName = ProjectGuid
        public static bool TryRead(string solutionFilename, ImmutableHashSet<string> projectFilter, out ImmutableArray<SolutionFileProjectInfo> projects)
        {
            // Get the serializer for the solution file
            var serializer = SolutionSerializers.GetSerializerByMoniker(solutionFilename);
            if (serializer == null)
            {
                projects = ImmutableArray<SolutionFileProjectInfo>.Empty;
                return false;
            }

            // The base directory for projects is the solution folder.
            var baseDirectory = Path.GetDirectoryName(solutionFilename);
            //RoslynDebug.AssertNotNull(baseDirectory);
            var solutionModel = serializer.OpenAsync(solutionFilename, CancellationToken.None).Result;

            //var builder = ImmutableArray.CreateBuilder<SolutionFileProjectInfo>(StringComparer.OrdinalIgnoreCase);
            var result = new List<SolutionFileProjectInfo>();
            foreach (var projectModel in solutionModel.SolutionProjects)
            {
                // If we are filtering based on a solution filter then we need to verify the project is included.
                if (!projectFilter.IsEmpty)
                {
                    // Removed PathResolver utility, as it only joins paths, plus some error handling options not relevant here. Basically directly passed to:
                    // NormalizeAbsolutePath(FileUtilities.ResolveRelativePath(path, baseDirectory) ?? path);
                    // The "??"-else case is not relevant for OmniSharp.
                    // ResolveRelativePath is essentially identical to Path.Join
                    //var absoluteProjectPath = Path.Join(baseDirectory, projectModel.FilePath);
                    // Path.Join is not available in .NET Framework
                    var absoluteProjectPath = Path.Combine(baseDirectory, projectModel.FilePath);
                    if (!File.Exists(absoluteProjectPath) || !projectFilter.Contains(absoluteProjectPath))
                    {
                        continue;
                    }
                }

                var configurations = new List<string>();
                if(projectModel.ProjectConfigurationRules.Count > 0)
                {
                    foreach(var configurationRule in projectModel.ProjectConfigurationRules)
                    {
                        configurations.Add($"{configurationRule.SolutionBuildType}|{configurationRule.SolutionPlatform}");
                    }
                }

                var project = new SolutionFileProjectInfo(
                    projectModel.FilePath,
                    projectModel.Id.ToString(),
                    configurations.ToImmutableHashSet()
                );
                result.Add(project);
            }

            projects = result.ToImmutableArray();
            return true;
        }
    }
}