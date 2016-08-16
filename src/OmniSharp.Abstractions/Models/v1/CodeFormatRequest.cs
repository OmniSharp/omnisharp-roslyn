using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.CodeFormat, typeof(CodeFormatRequest), typeof(CodeFormatResponse))]
    public class CodeFormatRequest : Request
    {
        /// <summary>
        ///  When true, return just the text changes.
        /// </summary>
        public bool WantsTextChanges { get; set; }
    }
}
