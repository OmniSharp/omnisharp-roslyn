namespace OmniSharp.Models
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
    }
}