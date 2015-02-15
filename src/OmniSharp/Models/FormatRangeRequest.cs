namespace OmniSharp.Models
{
    public class FormatRangeRequest : Request
    {
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
    }
}