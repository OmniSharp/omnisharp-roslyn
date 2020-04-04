namespace OmniSharp.Models.Events
{
    public class ProjectDiagnosticStatusMessage
    {
        public ProjectDiagnosticStatus Status { get; set; }
        public string ProjectFilePath { get; set; }
        public string Type = "background";
    }
}