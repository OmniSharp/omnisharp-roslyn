using System.Collections.Immutable;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal class SolutionFileProjectInfo
    {
        public string ProjectName { get; }
        public string ProjectGuid { get; }
        public ImmutableHashSet<string> SolutionConfigurations { get; set; }


        public SolutionFileProjectInfo(
            string projectName,
            string projectGuid,
            ImmutableHashSet<string> solutionConfigurations)
        {
            ProjectName = projectName;
            ProjectGuid = projectGuid;
            SolutionConfigurations = solutionConfigurations;
        }
    }
}