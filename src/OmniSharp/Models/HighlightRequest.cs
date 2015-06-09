namespace OmniSharp.Models
{
    public class HighlightRequest : Request
    {
        /// <summary>
        ///   Specifies which lines to highlight.
        ///   If none are given, highlight the entire
        ///   file.
        /// </summary>
        public int[] Lines { get; set; }

        /// <summary>
        ///   Specifies which projects to highlight for.
        //    If none are given, highlight for all the projects.
        /// </summary>
        public string[] ProjectNames { get; set; }
    }
}
