namespace OmniSharp.Models
{
    public class HighlightResponse
    {
        public int Line { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public string Kind { get; set; }
    }
}
