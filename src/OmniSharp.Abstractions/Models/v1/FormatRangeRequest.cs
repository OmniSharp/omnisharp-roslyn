using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/formatRange", typeof(FormatRangeRequest), typeof(FormatRangeResponse))]
    public class FormatRangeRequest : Request
    {
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
    }
}
