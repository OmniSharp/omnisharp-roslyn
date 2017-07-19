using System;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal class ProjectBlock
    {
        private const string SolutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

        // An example of a project line looks like this:
        //  Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ClassLibrary1", "ClassLibrary1\ClassLibrary1.csproj", "{DEBCE986-61B9-435E-8018-44B9EF751655}"
        private static readonly Lazy<Regex> s_lazyProjectHeader = new Lazy<Regex>(
            () => new Regex
                (
                "^" // Beginning of line
                + "Project\\(\"(?<PROJECTTYPEGUID>.*)\"\\)"
                + "\\s*=\\s*" // Any amount of whitespace plus "=" plus any amount of whitespace
                + "\"(?<PROJECTNAME>.*)\""
                + "\\s*,\\s*" // Any amount of whitespace plus "," plus any amount of whitespace
                + "\"(?<RELATIVEPATH>.*)\""
                + "\\s*,\\s*" // Any amount of whitespace plus "," plus any amount of whitespace
                + "\"(?<PROJECTGUID>.*)\""
                + "$", // End-of-line
                RegexOptions.Compiled)
            );

        public string ProjectTypeGuid { get; }
        public string ProjectName { get; }
        public string RelativePath { get; }
        public string ProjectGuid { get; }
        public ImmutableArray<SectionBlock> Sections { get; }

        public bool IsSolutionFolder => ProjectTypeGuid.Equals(SolutionFolderGuid, StringComparison.OrdinalIgnoreCase);

        private ProjectBlock(string projectTypeGuid, string projectName, string relativePath, string projectGuid, ImmutableArray<SectionBlock> sections)
        {
            ProjectTypeGuid = projectTypeGuid;
            ProjectName = projectName;
            RelativePath = relativePath;
            ProjectGuid = projectGuid;
            Sections = sections;
        }

        public static ProjectBlock Parse(string headerLine, Scanner scanner)
        {
            var match = s_lazyProjectHeader.Value.Match(headerLine);
            if (!match.Success)
            {
                return null;
            }

            var projectTypeGuid = match.Groups["PROJECTTYPEGUID"].Value.Trim();
            var projectName = match.Groups["PROJECTNAME"].Value.Trim();
            var relativePath = match.Groups["RELATIVEPATH"].Value.Trim();
            var projectGuid = match.Groups["PROJECTGUID"].Value.Trim();

            // If the project name is empty, set it to a generated generic value.
            if (string.IsNullOrEmpty(projectName))
            {
                projectName = "EmptyProjectName." + Guid.NewGuid();
            }

            if (relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                throw new InvalidSolutionFileException("A project path contains an invalid character.");
            }

            var sections = ImmutableArray.CreateBuilder<SectionBlock>();

            // Search for project dependencies. Keep reading until we either...
            // 1. reach the end of the file,
            // 2. see "ProjectSection( at the beginning of the line, or
            // 3. see "EndProject at the beginning of the line.

            string line;
            while ((line = scanner.NextLine()) != null)
            {
                if (line == "EndProject")
                {
                    break;
                }

                if (line.StartsWith("ProjectSection("))
                {
                    var section = ProjectSectionBlock.Parse(line, scanner);
                    if (section != null)
                    {
                        sections.Add(section);
                    }
                }
            }

            return new ProjectBlock(projectTypeGuid, projectName, relativePath, projectGuid, sections.ToImmutable());
        }
    }
}
