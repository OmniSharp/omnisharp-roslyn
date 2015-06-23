using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class MSBuildProjectDiagnostics
    {
        public string FileName { get; set; }
        public IEnumerable<MSBuildDiagnosticsMessage> Warnings { get; set; }
        public IEnumerable<MSBuildDiagnosticsMessage> Errors { get; set; }
    }
}