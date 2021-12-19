namespace OmniSharp.Models.Events
{
    public class BackgroundDiagnosticStatusMessage
    {
        public BackgroundDiagnosticStatus Status { get; set; }
        public int NumberProjects { get; set; }
        public int NumberFiles { get; set; }
        public int NumberFilesRemaining { get; set; }
    }
}
