using System.Collections.Immutable;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal sealed class ProjectSectionBlock : SectionBlock
    {
        private ProjectSectionBlock(string name, ImmutableArray<Property> properties)
            : base(name, properties)
        {
        }

        public static ProjectSectionBlock Parse(string headerLine, Scanner scanner)
        {
            var (name, properties) = ParseNameAndProperties(
                "ProjectSection", "EndProjectSection", headerLine, scanner);

            return new ProjectSectionBlock(name, properties);
        }
    }
}
