using System.Collections.Generic;
using OmniSharp.Models.V2;

namespace OmniSharp.Models.V2
{
    public class BlockStructureResponse
    {
        public IEnumerable<CodeFoldingBlock> Spans { get; set; }
    }
}
