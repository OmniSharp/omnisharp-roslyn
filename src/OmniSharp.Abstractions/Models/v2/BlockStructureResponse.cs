using System.Collections.Generic;
using OmniSharp.Models.V2;

namespace OmniSharp.Models.v2
{
    public class BlockStructureResponse
    {
        public IEnumerable<CodeFoldingBlock> Spans { get; set; }
    }
}
