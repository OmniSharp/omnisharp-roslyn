using OmniSharp.MSBuild.Logging;

namespace OmniSharp.MSBuild.Models.Events
{
    public class MSBuildDiagnosticsMessage
    {
        public string LogLevel { get; set; }
        public string FileName { get; set; }
        public string Text { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }

        internal static MSBuildDiagnosticsMessage FromDiagnostic(MSBuildDiagnostic diagnostic)
            => new MSBuildDiagnosticsMessage()
            {
                LogLevel = diagnostic.Severity.ToString(),
                FileName = diagnostic.File,
                Text = diagnostic.Message,
                StartLine = diagnostic.LineNumber,
                StartColumn = diagnostic.ColumnNumber,
                EndLine = diagnostic.EndLineNumber,
                EndColumn = diagnostic.EndColumnNumber
            };
    }
}
