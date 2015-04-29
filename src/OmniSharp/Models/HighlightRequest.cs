namespace OmniSharp.Models
{
    public class HighlightRequest : Request
    {
        /// <summary>
        ///   Spesifies which lines to highlight.
        ///   If none are given, highlight the entire
        ///   file.
        /// </summary>
        public int[] Lines { get; set; }
    }
}
