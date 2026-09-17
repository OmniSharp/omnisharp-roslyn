using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

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

        public static bool TryRead(string solutionFilename, ImmutableHashSet<string> projectFilter, out ImmutableArray<SolutionFileProjectInfo> projects)
        {
            var serializer = SolutionSerializers.GetSerializerByMoniker(solutionFilename);
            if (serializer == null)
            {
                projects = ImmutableArray<SolutionFileProjectInfo>.Empty;
                return false;
            }

            var absoluteSolutionPath = Path.GetFullPath(solutionFilename);
            var baseDirectory = Path.GetDirectoryName(absoluteSolutionPath);
            var solutionModel = serializer.OpenAsync(absoluteSolutionPath, CancellationToken.None).GetAwaiter().GetResult();
            var result = ImmutableArray.CreateBuilder<SolutionFileProjectInfo>();

            foreach (var projectModel in solutionModel.SolutionProjects)
            {
                var relativeProjectPath = projectModel.FilePath
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);
                var absoluteProjectPath = Path.GetFullPath(Path.Combine(baseDirectory, relativeProjectPath));
                if (!projectFilter.IsEmpty && !projectFilter.Contains(absoluteProjectPath))
                {
                    continue;
                }

                var configurations = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var solutionBuildType in solutionModel.BuildTypes)
                {
                    foreach (var solutionPlatform in solutionModel.Platforms)
                    {
                        var projectConfiguration = projectModel.GetProjectConfiguration(solutionBuildType, solutionPlatform);
                        if (projectConfiguration.BuildType != null && projectConfiguration.Platform != null)
                        {
                            configurations[$"{solutionBuildType}|{solutionPlatform}"] =
                                $"{projectConfiguration.BuildType}|{projectConfiguration.Platform}";
                        }
                    }
                }

                result.Add(new SolutionFileProjectInfo(
                    absoluteProjectPath,
                    projectModel.Id.ToString(),
                    configurations.ToImmutable()));
            }

            projects = result.ToImmutable();
            return true;
        }
    }
}