using System.Collections.Generic;

namespace OmniSharp.Models.V2
{
    public class DiagnosticMessage
    {
        public IEnumerable<DiagnosticResult> Results { get; set; }
    }
}