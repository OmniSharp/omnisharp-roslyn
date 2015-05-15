using System;
using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class FileMemberTree
    {
        public IEnumerable<FileMemberElement> TopLevelTypeDefinitions { get; set; }
    }
}
