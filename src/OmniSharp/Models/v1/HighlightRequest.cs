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
        /// <suimmary>
        ///   Request comment classifications
        /// </summary>
        /// <remarks>
        /// Defaults to true
        /// </remarks>
        public bool WantComments { get; set; } = true;
        /// <suimmary>
        ///   Request string classifications
        /// </summary>
        /// <remarks>
        /// Defaults to true
        /// </remarks>
        public bool WantStrings { get; set; } = true;
        /// <suimmary>
        ///   Request operator classifications
        /// </summary>
        /// <remarks>
        /// Defaults to true
        /// </remarks>
        public bool WantOperators { get; set; } = true;
        /// <suimmary>
        ///   Request punctuation classifications
        /// </summary>
        /// <remarks>
        /// Defaults to true
        /// </remarks>
        public bool WantPunctuation { get; set; } = true;
        /// <suimmary>
        ///   Request keyword classifications
        /// </summary>
        /// <remarks>
        /// Defaults to true
        /// </remarks>
        public bool WantKeywords { get; set; } = true;
        /// <suimmary>
        ///   Request number classifications
        /// </summary>
        /// <remarks>
        /// Defaults to true
        /// </remarks>
        public bool WantNumbers { get; set; } = true;
        /// <suimmary>
        ///   Request name classifications
        ///   (interface, class, enum, etc)
        /// </summary>
        /// <remarks>
        /// Defaults to true
        /// </remarks>
        public bool WantNames { get; set; } = true;
        /// <suimmary>
        ///   Request identifier classifications
        /// </summary>
        /// <remarks>
        /// Defaults to true
        /// </remarks>
        public bool WantIdentifiers { get; set; } = true;
        /// <suimmary>
        ///   Request keyword classifications
        /// </summary>
        /// <remarks>
        /// Defaults to true
        /// </remarks>
        public bool WantPreprocessorKeywords { get; set; } = true;
        /// <suimmary>
        ///   Request excluded code keyword classifications
        ///   (eg. frameworks in a dnx solution)
        /// </summary>
        /// <remarks>
        /// Defaults to true
        /// </remarks>
        public bool WantExcludedCode { get; set; } = true;
    }
}
