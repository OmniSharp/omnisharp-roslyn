using System.Collections.Generic;

namespace OmniSharp.Models.V2.CodeStructure
{
    public partial class CodeElement
    {
        public string Kind { get; }
        public string Name { get; }
        public string DisplayName { get; }
        public IReadOnlyList<CodeElement> Children { get; }
        public IReadOnlyList<CodeElementRange> Ranges { get; }
        public IReadOnlyDictionary<string, object> Properties { get; }

        private CodeElement(
            string kind, string name, string displayName,
            IReadOnlyList<CodeElement> children,
            IReadOnlyList<CodeElementRange> ranges,
            IReadOnlyDictionary<string, object> properties)
        {
            Kind = kind;
            Name = name;
            DisplayName = displayName;
            Children = children;
            Ranges = ranges;
            Properties = properties;
        }

        public override string ToString()
            => $"{Kind} {Name}";
    }
}
