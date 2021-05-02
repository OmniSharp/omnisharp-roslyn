using OmniSharp.Mef;
using OmniSharp.Models.V2;

namespace OmniSharp.Models.SemanticHighlight
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.Highlight, typeof(SemanticHighlightRequest), typeof(SemanticHighlightResponse))]
    public class SemanticHighlightRequest : Request
    {
        /// <summary>
        ///   Specifies the range to highlight.
        ///   If none is given, highlight the entire
        ///   file.
        /// </summary>
        public Range Range { get; set; }
    }
}
