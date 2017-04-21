using System.Collections.Generic;

namespace OmniSharp.Models.Diagnostics
{
    public class DiagnosticResult
    {
        public string FileName { get; set; }
        public IEnumerable<DiagnosticLocation> QuickFixes { get; set; }
    }
}