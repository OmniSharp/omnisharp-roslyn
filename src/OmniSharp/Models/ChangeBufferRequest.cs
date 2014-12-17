
namespace OmniSharp.Models
{
    public class ChangeBufferRequest
    {
        public string FileName { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string NewText { get; set; }
    }
}