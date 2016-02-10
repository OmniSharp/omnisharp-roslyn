using Newtonsoft.Json;
using OmniSharp.Json;

namespace OmniSharp.Models
{
    public class GotoDefinitionResponse
    {
        public string FileName { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Line { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Column { get; set; }
        public MetadataSource MetadataSource { get; set; }
    }
}
