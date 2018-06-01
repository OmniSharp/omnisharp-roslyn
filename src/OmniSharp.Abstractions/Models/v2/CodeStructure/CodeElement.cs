using System.Collections.Generic;

namespace OmniSharp.Models.V2.CodeStructure
{
    public class CodeElement
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public CodeElement[] Children { get; set; }
        public CodeElementRange[] Ranges { get; set; }
        public IDictionary<string, object> Properties { get; set; }

        public override string ToString()
            => $"{Kind} {Name}";
    }
}
