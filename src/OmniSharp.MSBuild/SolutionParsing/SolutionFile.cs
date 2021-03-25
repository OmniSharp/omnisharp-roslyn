using System;
using System.Collections.Immutable;
using System.IO;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal sealed class SolutionFile
    {
        public Version FormatVersion { get; }
        public Version VisualStudioVersion { get; }
        public ImmutableArray<ProjectBlock> Projects { get; }
        public ImmutableArray<GlobalSectionBlock> GlobalSections { get; }

        private SolutionFile(
            Version formatVersion,
            Version visualStudioVersion,
            ImmutableArray<ProjectBlock> projects,
            ImmutableArray<GlobalSectionBlock> globalSections)
        {
            FormatVersion = formatVersion;
            VisualStudioVersion = visualStudioVersion;
            Projects = projects;
            GlobalSections = globalSections;
        }

        public static SolutionFile Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            using (var scanner = new Scanner(text))
            {
                var formatVersion = ParseHeaderAndVersion(scanner);

                Version visualStudioVersion = null;

                var projects = ImmutableArray.CreateBuilder<ProjectBlock>();
                var globalSections = ImmutableArray.CreateBuilder<GlobalSectionBlock>();

                string line;
                while ((line = scanner.NextLine()) != null)
                {
                    if (line.StartsWith("Project(", StringComparison.Ordinal))
                    {
                        var project = ProjectBlock.Parse(line, scanner);
                        if (project != null)
                        {
                            projects.Add(project);
                        }
                    }
                    else if (line.StartsWith("GlobalSection(", StringComparison.Ordinal))
                    {
                        var globalSection = GlobalSectionBlock.Parse(line, scanner);
                        if (globalSection != null)
                        {
                            globalSections.Add(globalSection);
                        }
                    }
                    else if (line.StartsWith("VisualStudioVersion", StringComparison.Ordinal))
                    {
                        visualStudioVersion = ParseVisualStudioVersion(line);
                    }
                }

                return new SolutionFile(formatVersion, visualStudioVersion, projects.ToImmutable(), globalSections.ToImmutable());
            }
        }

        public static SolutionFile ParseFile(string path)
        {
            var text = File.ReadAllText(path);
            return Parse(text);
        }

        private static Version ParseHeaderAndVersion(Scanner scanner)
        {
            const string HeaderPrefix = "Microsoft Visual Studio Solution File, Format Version ";

            // Read the file header. This can be on either of the first two lines.
            for (var i = 0; i < 2; i++)
            {
                var line = scanner.NextLine();
                if (line == null)
                {
                    break;
                }

                if (line.StartsWith(HeaderPrefix, StringComparison.Ordinal))
                {
                    // Found the header. Now get the version.
                    var lineEnd = line.Substring(HeaderPrefix.Length);

                    if (Version.TryParse(lineEnd, out var version))
                    {
                        return version;
                    }

                    return null;
                }
            }

            // If we got here, we didn't find the file header on either the first or second line.
            throw new InvalidSolutionFileException("Solution header should be on first or second line.");
        }

        private static Version ParseVisualStudioVersion(string line)
        {
            // The version line should look like:
            //
            // VisualStudioVersion = 15.0.26228.4

            var tokens = line.Split(new[] { ' ', '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2)
            {
                var versionText = tokens[1];
                if (Version.TryParse(versionText, out var result))
                {
                    return result;
                }
            }

            return null;
        }
    }
}
