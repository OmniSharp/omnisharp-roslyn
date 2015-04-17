using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class ProjectStatusMessage
    {
        public static ProjectStatusMessage Warning(string filename, string message = null)
        {
            return new ProjectStatusMessage()
            {
                FileName = filename,
                LogLevel = "warning",
                Text = message
            };
        }

        public static ProjectStatusMessage Error(string filename, string message = null)
        {
            return new ProjectStatusMessage()
            {
                FileName = filename,
                LogLevel = "error",
                Text = message
            };
        }

        public string FileName { get; set; }
        public string LogLevel { get; set; }
        public string Text { get; set; }
        public string ExceptionMessage { get; set; }
        public IEnumerable<DiagnosticLocation> Diagnostics { get; set; }
    }
}