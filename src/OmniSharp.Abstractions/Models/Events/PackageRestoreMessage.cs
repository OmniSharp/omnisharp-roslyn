namespace OmniSharp.Models.Events
{
    public class PackageRestoreMessage
    {
        public string FileName { get; set; }
        public bool Succeeded { get; set; }
    }
}