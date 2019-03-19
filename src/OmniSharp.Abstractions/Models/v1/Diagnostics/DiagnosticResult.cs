using System.Collections.Generic;

namespace OmniSharp.Models.Diagnostics
{
    public class DiagnosticResult
    {
        public string FileName { get; set; }
        public IEnumerable<DiagnosticLocation> QuickFixes { get; set; }

        public override string ToString()
        {
            return $"{FileName} -> {string.Join(", ", QuickFixes)}";
        }
    }
}
