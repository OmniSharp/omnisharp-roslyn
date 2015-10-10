using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.Models.V2
{
    public class DiagnosticMessage : DiagnosticLocation
    {
        public bool Clear { get; set; }
    }
}
