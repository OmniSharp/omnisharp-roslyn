using OmniSharp.Mef;

namespace OmniSharp.Models.CodeFormat
{
    [OmniSharpEndpoint(OmniSharpEndpoints.CodeFormat, typeof(CodeFormatRequest), typeof(CodeFormatResponse))]
    public class CodeFormatRequest : Request
    {
        /// <summary>
        ///  When true, return just the text changes.
        /// </summary>
        public bool WantsTextChanges { get; set; }
    }
}
