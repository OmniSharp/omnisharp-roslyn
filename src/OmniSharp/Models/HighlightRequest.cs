namespace OmniSharp.Models
{
    public class HighlightRequest : Request
    {
        /// <summary>
        ///   Specifies which lines to highlight.
        ///   If none are given, highlight the entire
        ///   file.
        /// </summary>
        /// <remarks>
        ///   This is 0 indexed.
        /// </remarks>
        public int[] Lines { get; set; }
    }
}
