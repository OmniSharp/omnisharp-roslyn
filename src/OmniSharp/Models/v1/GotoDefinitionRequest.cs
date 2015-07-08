using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.Models
{
    public class GotoDefinitionRequest : Request
    {
        public bool WantMetadataSource { get; set; }
    }
}
