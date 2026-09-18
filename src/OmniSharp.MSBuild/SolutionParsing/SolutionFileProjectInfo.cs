using System.Collections.Generic;
using System.Collections.Immutable;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal sealed class SolutionFileProjectInfo
    {
        public string ProjectPath { get; }
        public string ProjectGuid { get; }
        public IReadOnlyDictionary<string, string> SolutionConfigurations { get; }

        public SolutionFileProjectInfo(
            string projectPath,
            string projectGuid,
            ImmutableDictionary<string, string> solutionConfigurations)
        {
            ProjectPath = projectPath;
            ProjectGuid = projectGuid;
            SolutionConfigurations = solutionConfigurations;
        }
    }
}