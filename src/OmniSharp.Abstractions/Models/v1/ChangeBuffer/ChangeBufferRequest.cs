using Newtonsoft.Json;
using OmniSharp.Mef;

namespace OmniSharp.Models.ChangeBuffer
{
    [OmniSharpEndpoint(OmniSharpEndpoints.ChangeBuffer, typeof(ChangeBufferRequest), typeof(object))]
    public class ChangeBufferRequest : IRequest
    {
        public string FileName { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int StartLine { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int StartColumn { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int EndLine { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int EndColumn { get; set; }
        public string NewText { get; set; }
    }
}
