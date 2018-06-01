using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OmniSharp.Models.V2;

namespace OmniSharp.Models.v2
{
    public class BlockStructureResponse
    {
        public IEnumerable<BlockSpan> Spans { get; set; }
    }
}
