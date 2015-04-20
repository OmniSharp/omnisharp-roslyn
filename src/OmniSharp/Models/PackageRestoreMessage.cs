namespace OmniSharp.Models
{
    public class PackageRestoreMessage
    {
        public string ProjectFileName { get; set; }
        public bool Success { get; set; }
    }
}