using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniSharp.Models.TypeLookup
{
    public class DocumentationItem
    {
        public string Name { get; }
        public string Documentation { get; }
        public DocumentationItem(string name, string documentation)
        {
            Name = name;
            Documentation = documentation;
        }
    }
}
