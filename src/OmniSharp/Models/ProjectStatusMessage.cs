using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class ProjectStatusMessage
    {
        public string FileName { get; set; }
        public string LogLevel { get; set; }
        public string Text { get; set; }
        public string ExceptionMessage { get; set; }
        public IEnumerable<DiagnosticLocation> Diagnostics { get; set; }
    }
}