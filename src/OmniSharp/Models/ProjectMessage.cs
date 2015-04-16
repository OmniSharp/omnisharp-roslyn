namespace OmniSharp.Models
{
    public class ProjectMessage
    {
        public ProjectMessageSeverity Severity { get; set; }

        public string FileName { get; set; }

        public string Message { get; set; }
    }
}