using Newtonsoft.Json;
using OmniSharp.Json;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.FormatRange, typeof(FormatRangeRequest), typeof(FormatRangeResponse))]
    public class FormatRangeRequest : Request
    {
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int EndLine { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int EndColumn { get; set; }
    }
}
