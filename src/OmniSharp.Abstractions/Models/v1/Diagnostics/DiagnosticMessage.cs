using System.Collections.Generic;

namespace OmniSharp.Models.Diagnostics
{
    public class DiagnosticMessage
    {
        public IEnumerable<DiagnosticResult> Results { get; set; }
    }
}