using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class DiagnosticMessage
    {
        public IEnumerable<DiagnosticResult> Results { get; set; }
    }
}