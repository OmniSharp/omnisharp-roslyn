using System;
using System.Collections.Immutable;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal abstract class SectionBlock
    {
        public string Name { get; }
        public ImmutableArray<Property> Properties { get; }

        protected SectionBlock(string name, ImmutableArray<Property> properties)
        {
            Name = name;
            Properties = properties;
        }

        protected static (string name, ImmutableArray<Property> properties) ParseNameAndProperties(
            string startSection, string endSection, string headerLine, Scanner scanner)
        {
            var startIndex = startSection.Length;
            if (!startSection.EndsWith("("))
            {
                startIndex++;
            }

            var endIndex = headerLine.IndexOf(')', startIndex);
            var name = endIndex >= startIndex
                ? headerLine.Substring(startIndex, endIndex - startIndex)
                : headerLine.Substring(startIndex);

            var properties = ImmutableArray.CreateBuilder<Property>();

            string line;
            while ((line = scanner.NextLine()) != null)
            {
                if (line.StartsWith(endSection, StringComparison.Ordinal))
                {
                    break;
                }

                var property = Property.Parse(line);
                if (property != null)
                {
                    properties.Add(property);
                }
            }

            return (name, properties.ToImmutable());
        }
    }
}
