using System.Collections.Generic;
using Newtonsoft.Json;

namespace OmniSharp.Models
{
    public class Request : SimpleFileRequest
    {
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Line { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Column { get; set; }
        public string Buffer { get; set; }
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool ApplyChangesTogether { get; set; }
    }
}
