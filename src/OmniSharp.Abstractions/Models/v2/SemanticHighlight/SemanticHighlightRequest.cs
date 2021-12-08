using OmniSharp.Mef;
using OmniSharp.Models.V2;

namespace OmniSharp.Models.SemanticHighlight
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.Highlight, typeof(SemanticHighlightRequest), typeof(SemanticHighlightResponse))]
    public class SemanticHighlightRequest : Request
    {
        /// <summary>
        ///   Specifies the range to highlight. If none is given, highlight the entire file.
        /// </summary>
        public Range Range { get; set; }

        /// <summary>
        ///   Optionally provide the text for a different version of the document to be highlighted.
        ///   This property works differently than the Buffer property, since it is only used for
        ///   highlighting and will not update the document in the CurrentSolution.
        /// </summary>
        public string VersionedText { get; set; }
    }
}
