namespace OmniSharp.Models
{
    public class RenameRequest : Request
    {
        public bool WantsTextChanges { get; set; }

        public string RenameTo { get; set; }
    }
}