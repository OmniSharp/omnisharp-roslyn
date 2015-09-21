using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/highlight", typeof(HighlightRequest), typeof(HighlightResponse))]
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
        /// <summary>
        ///   Request specific classifications, if none are requested you will get them all.
        /// </summary>
        public HighlightClassification[] Classifications { get; set; }
        /// <summary>
        ///   Exclude specific classifications
        /// </summary>
        public HighlightClassification[] ExcludeClassifications { get; set; }
    }
}
