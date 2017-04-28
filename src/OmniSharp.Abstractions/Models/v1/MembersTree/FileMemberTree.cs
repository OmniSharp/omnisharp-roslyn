using System.Collections.Generic;

namespace OmniSharp.Models.MembersTree
{
    public class FileMemberTree
    {
        public IEnumerable<FileMemberElement> TopLevelTypeDefinitions { get; set; }
    }
}
