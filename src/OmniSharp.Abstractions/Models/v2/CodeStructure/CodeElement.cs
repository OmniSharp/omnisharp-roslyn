using System.Collections.Generic;

namespace OmniSharp.Models.V2.CodeStructure
{
    public class CodeElement
    {
        public string Kind { get; set; }
        public string Accessibility { get; set; }
        public CodeElementRange[] Ranges { get; set; }
        public IDictionary<string, object> Properties { get; set; }
    }
}
